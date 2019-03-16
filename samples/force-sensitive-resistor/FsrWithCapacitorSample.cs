﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Device.Gpio;
using System.Diagnostics;
using System.Threading;

namespace force_sensitive_resistor
{
    class FsrWithCapacitorSample
    {
        private GpioController _controller;
        private int _pinNumber = 18; // set the pin number reading

        public FsrWithCapacitorSample()
        {
            _controller = new GpioController();
            _controller.OpenPin(_pinNumber);
        }

        private long ReadCapacitorChargingDuration()
        {
            int count = 0;
            _controller.SetPinMode(_pinNumber, PinMode.Output);
            // set pin to low
            _controller.Write(_pinNumber, PinValue.Low);
            // Prepare pin for input and wait until high read
            _controller.SetPinMode(_pinNumber, PinMode.Input);

            while (_controller.Read(_pinNumber) == PinValue.Low)
            {   // count until read High
                count++;
                if (count == 30000)
                {   // if count goes too high it means FSR in highest resistance which means no pressure, do not need to count more
                    break;
                }
            }
            return count;
        }

        public void StartReading()
        {
            while (true)
            {
                long value = ReadCapacitorChargingDuration();

                if (value == 30000)
                {   // 30000 is count limit, if we got this count it means Fsr has its highest resistance, so it is not pressed
                    Console.WriteLine("Not pressed");
                }
                else
                {
                    Console.WriteLine("Pressed");
                }
                Thread.Sleep(500);
            }
        }
    }
}