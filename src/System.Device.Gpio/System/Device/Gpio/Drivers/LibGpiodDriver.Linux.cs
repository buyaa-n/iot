﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading.Tasks;

namespace System.Device.Gpio.Drivers
{
    public class LibGpiodDriver : UnixDriver
    {
        private string _deviceName = "gpiochip0";

        private SafeChipHandle _chip;

        private Dictionary<int, SafeLineHandle> _pinNumberToSafeLineHandle;

        private timespec _defaultTimeOut = new timespec(IntPtr.Zero, new IntPtr(1_000_000)); // one millisecond

        private Dictionary<int, LibGpiodDriverEventHandler> _pinNumberToEventHandler = new Dictionary<int, LibGpiodDriverEventHandler>();

        protected internal override int PinCount => Interop.gpiod_chip_num_lines(_chip);

        public LibGpiodDriver()
        {
            OpenChip();
            _pinNumberToSafeLineHandle = new Dictionary<int, SafeLineHandle>(PinCount);
        }

        private void OpenChip()
        {
            SafeChipIteratorHandle iterator = Interop.gpiod_chip_iter_new();
            if (iterator == null)
            {
                throw new IOException($"No any chip available, error code: {Marshal.GetLastWin32Error()}");
            }
            _chip = Interop.gpiod_chip_iter_next(iterator);
            if (_chip == null)
            {
                throw new IOException($"No chip found, error code: {Marshal.GetLastWin32Error()}");
            }
            _deviceName = Marshal.PtrToStringAnsi(Interop.gpiod_chip_name(_chip));
            // Freeing other chips opened
            Interop.gpiod_chip_iter_free_noclose(iterator);
        }

        private void RequestEvent(int pinNumber, LibGpiodDriverEventHandler eventDescriptor, PinEventTypes eventType)
        {
            int eventSuccess = -1;
            SafeLineHandle pin = Interop.gpiod_chip_get_line(_chip, pinNumber);
            if (pin != null)
            {
                eventDescriptor.PinHandle = pin;
                if (eventType == PinEventTypes.Rising || eventType == PinEventTypes.Falling)
                {
                    eventSuccess = Interop.gpiod_line_request_both_edges_events(pin, $"Listen {pinNumber} for falling edge event");
                }
                else
                {
                    throw new ArgumentException("Invalid or not supported event type requested");
                }
            }
            if (eventSuccess < 0)
            {
                throw new IOException($"Error while requesting event listener for pin {pinNumber}, error code: {Marshal.GetLastWin32Error()}");
            }
        }

        protected internal override void AddCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventType, PinChangeEventHandler callback)
        {
            if (!_pinNumberToEventHandler.TryGetValue(pinNumber, out LibGpiodDriverEventHandler eventHandler))
            {
                if (_pinNumberToSafeLineHandle.TryGetValue(pinNumber, out SafeLineHandle pin))
                    Interop.gpiod_line_release(pin);
                eventHandler = new LibGpiodDriverEventHandler(pinNumber, new CancellationTokenSource());
                lock (_pinNumberToEventHandler)
                {
                    _pinNumberToEventHandler.Add(pinNumber, eventHandler);
                }
                RequestEvent(pinNumber, eventHandler, eventType);
                InitializeEventDetectionTask(eventHandler);
            }
            if (eventType == PinEventTypes.Rising)
            {
                eventHandler.ValueRising += callback;
            }
            else if (eventType == PinEventTypes.Falling)
            {
                eventHandler.ValueFalling += callback;
            }
        }

        private void InitializeEventDetectionTask(LibGpiodDriverEventHandler eventHandler)
        {
            Task pinListeningThread = Task.Run(() =>
            {
                int waitResult = -2;
                while (!eventHandler.CancellationTokenSource.IsCancellationRequested)
                {
                    waitResult = Interop.gpiod_line_event_wait(eventHandler.PinHandle, ref _defaultTimeOut);
                    if (waitResult == -1)
                    {
                        throw new IOException($"Error while waiting for event, error code: {Marshal.GetLastWin32Error()}");
                    }
                    if (waitResult == 1)
                    {
                        int readResult = Interop.gpiod_line_event_read(eventHandler.PinHandle, out gpiod_line_event eventInfo);
                        if (readResult == -1)
                        {
                            throw new IOException($"Error while trying to read pin event result, error code: {Marshal.GetLastWin32Error()}");
                        }
                        PinEventTypes eventType = (eventInfo.event_type == 1) ? PinEventTypes.Rising : PinEventTypes.Falling;
                        var args = new PinValueChangedEventArgs(eventType, eventHandler.PinNumber);
                        eventHandler?.OnPinValueChanged(args, eventType);
                    }
                }
            });
        }

        protected internal override void ClosePin(int pinNumber)
        {
            if (_pinNumberToSafeLineHandle.TryGetValue(pinNumber, out SafeLineHandle pin))
            {
                Interop.gpiod_line_release(pin);
                _pinNumberToSafeLineHandle.Remove(pinNumber);
            }
        }

        protected internal override int ConvertPinNumberToLogicalNumberingScheme(int pinNumber) => throw new
            PlatformNotSupportedException("This driver is generic so it cannot perform conversions between pin numbering schemes.");

        protected internal override PinMode GetPinMode(int pinNumber)
        {
            if (_pinNumberToSafeLineHandle.TryGetValue(pinNumber, out SafeLineHandle pin))
            {
                return (Interop.gpiod_line_direction(pin) == 1) ? PinMode.Input : PinMode.Output;
            }
            return PinMode.Input;
        }

        protected internal override bool IsPinModeSupported(int pinNumber, PinMode mode)
        {
            // Libgpiod Api seems do not support pull up or pull down resistors.
            return mode != PinMode.InputPullDown && mode != PinMode.InputPullUp;
        }

        protected internal override void OpenPin(int pinNumber)
        {
            SafeLineHandle pin = Interop.gpiod_chip_get_line(_chip, pinNumber);
            if (pin == null)
            {
                throw new IOException($"Pin number {pinNumber} not available for chip: {_chip} error: {Marshal.GetLastWin32Error()}");
            }
            _pinNumberToSafeLineHandle.Add(pinNumber, pin);
        }

        protected internal override PinValue Read(int pinNumber)
        {
            if (_pinNumberToSafeLineHandle.TryGetValue(pinNumber, out SafeLineHandle pin))
            {
                int value = Interop.gpiod_line_get_value(pin);
                if (value == -1)
                {
                    throw new IOException($"Error while reading value from pin number: {pinNumber}, error: {Marshal.GetLastWin32Error()}");
                }
                return (value == 1) ? PinValue.High : PinValue.Low;
            }
            return PinValue.Low;
        }

        protected internal override void RemoveCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback)
        {
            if (_pinNumberToEventHandler.TryGetValue(pinNumber, out LibGpiodDriverEventHandler eventHandler))
            {
                eventHandler.ValueFalling -= callback;
                eventHandler.ValueRising -= callback;
                if (eventHandler.IsCallbackListEmpty())
                {
                    eventHandler.CancellationTokenSource.Cancel();
                    Interop.gpiod_line_release(eventHandler.PinHandle);
                    eventHandler.Dispose();
                    lock (_pinNumberToEventHandler)
                    {
                        _pinNumberToEventHandler.Remove(pinNumber);
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("Attempted to remove a callback for a pin that is not listening for events.");
            }
        }

        protected internal override void SetPinMode(int pinNumber, PinMode mode)
        {
            int success = -1;
            if (_pinNumberToSafeLineHandle.TryGetValue(pinNumber, out SafeLineHandle pin))
            {
                if (mode == PinMode.Input)
                {
                    success = Interop.gpiod_line_request_input(pin, pinNumber.ToString());
                }
                else
                {
                    success = Interop.gpiod_line_request_output(pin, pinNumber.ToString(), 0);
                }
            }
            if (success == -1) {
                throw new IOException($"Error setting pin mode, pin:{pinNumber}, error: {Marshal.GetLastWin32Error()}");
            } 
        }

        protected internal override WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventType, CancellationToken cancellationToken)
        {
            bool timedOut = false;
            if (_pinNumberToSafeLineHandle.TryGetValue(pinNumber, out SafeLineHandle pin))
            {
                Interop.gpiod_line_release(pin);
            }
            int eventRegistered = Interop.gpiod_ctxless_event_loop(_deviceName, (uint)pinNumber, false, $"pin{pinNumber} listener", ref _defaultTimeOut, null,
                (int e, uint p, ref timespec t, IntPtr d) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        timedOut = true;
                        return 1;
                    }
                    // the order of rising event for the enum passed to the callback (e) is 2, which is 1 in our PinEventTypes, 
                    // so was need to plus 1 to our event order  
                    if (e == (int)eventType + 1) 
                    {
                        return 1;
                    }
                    return 0;
                }, IntPtr.Zero);
            if (eventRegistered == 0)
            {
                return new WaitForEventResult
                {
                    TimedOut = timedOut,
                    EventType = eventType
                };
            }
            throw new IOException($"Failed to regsiter event {eventType} to pin {pinNumber} error: {Marshal.GetLastWin32Error()}");
        }

        protected internal override void Write(int pinNumber, PinValue value)
        {
            if (_pinNumberToSafeLineHandle.TryGetValue(pinNumber, out SafeLineHandle pin))
            {
                Interop.gpiod_line_set_value(pin, (value == PinValue.High) ? 1 : 0);
            }
        }

        protected override void Dispose(bool disposing)
        {
            foreach (SafeLineHandle handle in _pinNumberToSafeLineHandle.Values)
            {
                if (handle != null)
                {
                    Interop.gpiod_line_release(handle);
                }
            }
            foreach (LibGpiodDriverEventHandler devicePin in _pinNumberToEventHandler.Values)
            {
                devicePin.Dispose();
            }
            _pinNumberToEventHandler.Clear();
            if (_chip != null) 
                _chip.Dispose();
            base.Dispose(disposing);
        }
    }
}
