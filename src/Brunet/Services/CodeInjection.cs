/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007  David Wolinsky <davidiw@ufl.edu>, University of Florida
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
using System.IO;
using System.Reflection;

using Brunet.Messaging;
namespace Brunet.Services
{
  /**
   * Code injection allows the ability for dlls to be added to Brunet at
   * runtime as well as allow site specific code additions without the
   * need to recompile entry point code.  All injected code must be class
   * type "Injected" and implement base class "BaseInjected".
   */
  public class CodeInjection
  {
    protected Node _node;

    public CodeInjection(Node node)
    {
      _node = node;
      _node.Rpc.AddHandler("CodeInjection", this);
    }

    /**
     * Loads all the modules that begin with the name "Brunet.Inject." in the
     * present directory.  Make sure to execute your entry point from  the
     * directory that has these files.
     */
    public void LoadLocalModules()
    {
      try {
        string [] files = Directory.GetFiles(Directory.GetCurrentDirectory(), "Brunet.Inject*");
        foreach(string file in files) {
          try {
            this.Inject(file);
          }
          catch (Exception e){Console.WriteLine(e);}
        }
      }
      catch (Exception e){Console.WriteLine(e);}
    }

    /**
     * Injects the file at the specified assembly_name location.
     * @param assembly_name a file name and optional path to a module to 
     * inject.  The default path is the current directory.
     */
    public void Inject(string assembly_name)
    {
      byte[] assembly_data = null;
      using (FileStream fs = File.Open(assembly_name, FileMode.Open)) {
        assembly_data = new byte[fs.Length];
        fs.Read(assembly_data, 0, assembly_data.Length);
      }
      var ad = Brunet.Util.MemBlock.Reference(assembly_data);
      this.Inject(ad);
    }

    /**
     * Injects the binary data into the system.
     * @param assembly_data pre-compiled data that will be injected into the
     * system.
     */
    protected void Inject(Brunet.Util.MemBlock assembly_data)
    {
      Assembly ass = Assembly.Load(assembly_data);
      Type[] types = ass.GetTypes();
      foreach(Type type in types) {
        ass.CreateInstance(type.ToString(), false, BindingFlags.CreateInstance,
                           null, new object[1] {_node}, null, null);
      }
    }
  }
}
