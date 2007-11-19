/*
This program is part of BruNet, a library for the creation of efficient overlay
networks.
Copyright (C) 2007  David Wolinsky <davidiw@ufl.edu>, University of Florida
This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

using System;
using System.IO;
using System.Reflection;

namespace Brunet
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
    }

    /**
     * Loads all the modules that begin with the name "Brunet.Inject." in the
     * present directory.  Make sure to execute your entry point from  the
     * directory that has these files.
     */
    public void LoadLocalModules()
    {
      string [] files = Directory.GetFiles(".", "Brunet.Inject.*");
      foreach(string file in files) {
        try {
          this.Inject(file);
        }
        catch{}
      }
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
      MemBlock ad = MemBlock.Reference(assembly_data);
      this.Inject(ad);
    }

    /**
     * Injects the binary data into the system.
     * @param assembly_data pre-compiled data that will be injected into the
     * system.
     */
    public void Inject(MemBlock assembly_data)
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
