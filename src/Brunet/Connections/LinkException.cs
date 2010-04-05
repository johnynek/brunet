/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2005  University of California

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;

namespace Brunet.Connections {

  /**
   * Lots of things can go wrong in the Linker.  When something
   * goes wrong, the Linker throws a LinkException.
   *
   * All LinkExceptions should be caught inside Linker.
   */
  public class LinkException : Exception {
 
    protected bool _critical = false;
    //this is true if we should halt the current link protocol
    public bool IsCritical { get { return _critical; } }
   
    protected ErrorMessage _em;
    public ErrorMessage EMToSend {
      get { return _em; }
    }
    
    public LinkException():base()
    {
    }
    public LinkException(string message):base(message)
    {
    }
    public LinkException(string message, bool crit, ErrorMessage em) : base(message) {
      _critical = crit;
      _em = em;
    }
    public LinkException(string mes, Exception inner):base(mes, inner)
    {
    }

  }
}
