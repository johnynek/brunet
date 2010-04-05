/*
Copyright (C) 2008  David Wolinsky <davidiw@ufl.edu>, University of Florida

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
