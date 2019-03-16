// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Iot.Device.Mcp3008;

namespace force_sensitive_resistor
{
    class FsrWithAdcSample
    {
        private int _pinNumber = 0; // pin number to which FSR output connected
        private int _resistance { get; set; } = 10_000; // kOhm
        private int _voltageSupplied { get; set; } = 3_300;  // 3300mV = 3.3V


        private int CalculateVoltage(int readValue)
        {
            // This sample used Mcp3008 ADC which analog voltage read output ranges from 0 to 1023 (10 bit) 
            // mapping it to corresponding milli voltage, update output range if you use different ADC
            return _voltageSupplied * readValue / 1023;
        }

        private int CalculateFsrResistance(int fsrVoltage)
        {
            // Formula: FSR = ((Vcc - V) * R) / V
            if (fsrVoltage > 0)
            {
                return (_voltageSupplied - fsrVoltage) * _resistance / fsrVoltage;
            }
            return 0;
        }


        private int CalculateForce(int resistance)
        {
            if (resistance > 0)
            {
                int force;
                int fsrConductance = 1_000_000 / resistance; // in micro ohms

                // Use the two FSR guide graphs to approximate the force
                if (fsrConductance <= 1000)
                {
                    force = fsrConductance / 80;
                }
                else
                {
                    force = fsrConductance - 1000;
                    force /= 30;
                }
                return force;
            }
            return 0;
        }

        public void StartReading() {
            // Create a ADC convertor instance you are using depending how you wired ADC pins to controller
            // in this example used ADC Mcp3008 with "bit-banging" wiring method.
            // please refer https://github.com/dotnet/iot/tree/master/src/devices/Mcp3008/samples for more information

            using (Mcp3008 adcConvertor = new Mcp3008(18, 23, 24, 25))
            {
                while (true)
                {
                    int value = adcConvertor.Read(_pinNumber);
                    int voltage = CalculateVoltage(value);
                    int resistance = CalculateFsrResistance(voltage);
                    int force = CalculateForce(resistance);
                    Console.WriteLine($"Read value: {value}, voltage: {voltage}, resistance: {resistance}, approximate force in Newtons: {force}");
                    Thread.Sleep(500);
                }
            }
        }
    }
}
