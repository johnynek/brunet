/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.Security.Cryptography;
using System.Text;

namespace Brunet.Security.Utils {

public class keymaker {
  public static void Main(String []args) {
    int keysize = 1024;
    if(args.Length == 1) {
      try {
        keysize = Int32.Parse(args[0]);
      }
      catch { 
        Console.WriteLine("Default key size is 1024, specify 512 or 2048 as an input parameter.");
        return;
      }
    } 
    RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(keysize);
    // Create public key file
    byte[] rsa_public = rsa.ExportCspBlob(false);
    FileStream public_file = File.Open("rsa_public", FileMode.Create);
    public_file.Write(rsa_public, 0, rsa_public.Length);
    public_file.Close();
    // Create private key file
    byte[] rsa_private = rsa.ExportCspBlob(true);
    FileStream private_file = File.Open("rsa_private", FileMode.Create);
    private_file.Write(rsa_private, 0, rsa_private.Length);
    private_file.Close();
  }
}

}
