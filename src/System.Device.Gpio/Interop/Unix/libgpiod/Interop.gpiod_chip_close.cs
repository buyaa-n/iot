﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System;
using System.Runtime.InteropServices;

internal partial class Interop
{
    private const string library = "libgpiod";
    [DllImport(library, SetLastError = true)]
    internal static extern void gpiod_chip_close(IntPtr chip);
}