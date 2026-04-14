// MIT License
// Copyright (c) Indi.An GmbH
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// enum describes the type for history read operation.
/// </summary>
public enum HistoryReadOperation
{
    /// <summary>
    /// Subscribe to data changes.
    /// </summary>
    Subscribe = 1,

    /// <summary>
    /// Read raw data.
    /// </summary>
    Raw = 2,

    /// <summary>
    /// Read modified data.
    /// </summary>
    Modified = 3,

    /// <summary>
    /// Read data at the specified times.
    /// </summary>
    AtTime = 4,

    /// <summary>
    /// Read processed data.
    /// </summary>
    Processed = 5,

    /// <summary>
    /// Insert data.
    /// </summary>
    Insert = 6,

    /// <summary>
    /// Insert or replace data.
    /// </summary>
    InsertReplace = 7,

    /// <summary>
    /// Replace data.
    /// </summary>
    Replace = 8,

    /// <summary>
    /// Remove data.
    /// </summary>
    Remove = 9,

    /// <summary>
    /// Delete raw data.
    /// </summary>
    DeleteRaw = 10,

    /// <summary>
    /// Delete modified data.
    /// </summary>
    DeleteModified = 11,

    /// <summary>
    /// Delete data at the specified times.
    /// </summary>
    DeleteAtTime = 12,


    /// <summary>
    /// exit command
    /// </summary>
    exit =13
   
}
