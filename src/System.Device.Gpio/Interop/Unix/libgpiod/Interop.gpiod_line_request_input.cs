﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System;
using System.Device.Gpio;
using System.Runtime.InteropServices;

internal partial class Interop
{
    /// <summary>
    ///     Reserve a single line, set the direction to input.
    ///     <see href="https://git.kernel.org/pub/scm/libs/libgpiod/libgpiod.git/tree/include/gpiod.h#n823">here</see>.
    /// </summary>
    /// <param name="line">GPIO line handle</param>
    /// <param name="consumer">Name of the consumer.</param>
    /// <returns>0 if the line was properly reserved, -1 on failure.</returns>
    [DllImport(LibgpiodLibrary)]
    internal static extern int gpiod_line_request_input(SafeLineHandle line, string consumer);
}