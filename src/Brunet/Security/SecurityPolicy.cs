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
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Brunet.Security {
  /// <summary>Each SP supports a specific SymmetricAlgorithm and HashAlgorithm.  This class
  /// is thread-safe.</summary>
  public class SecurityPolicy {
    public static readonly Dictionary<int, SecurityPolicy> _splist;
    public static object _ssync;
    public static readonly int DefaultSPI = 0;
    public static readonly string DefaultEncryptor = "Rijndael";
    public static readonly string DefaultAuthenticator = "SHA1";
    public static readonly bool DefaultPreExchangedKeys = false;

    /// <summary>The SPI</summary>
    public readonly int Index;
    /// <summary>The supported HashAlgorithm.</summary>
    public readonly string Authenticator;
    /// <summary>The supported SymmetricAlgorithm.</summary>
    public readonly string Encryptor;
    /// <summary>Supports Pre-Exchanged Keys.</summary>
    public readonly bool PreExchangedKeys;

    static SecurityPolicy() {
      _ssync = new object();
      _splist = new Dictionary<int, SecurityPolicy>();
      SetDefaultSecurityPolicy(DefaultEncryptor, DefaultAuthenticator, DefaultPreExchangedKeys);
    }

    public static void SetDefaultSecurityPolicy(string Encryptor, string Authenticator,
        bool PreExchangedKeys) {
      lock(_ssync) {
        _splist.Remove(DefaultSPI);
        new SecurityPolicy(DefaultSPI, Encryptor, Authenticator, PreExchangedKeys);
      }
    }

    public SecurityPolicy(int Index, string Encryptor, string Authenticator) :
      this(Index, Encryptor, Authenticator, false) {
    }
    public SecurityPolicy(int Index, string Encryptor, string Authenticator, 
        bool PreExchangedKeys) {
      this.Index = Index;
      this.Encryptor = Encryptor;
      this.Authenticator = Authenticator;
      this.PreExchangedKeys = PreExchangedKeys;

      lock(_ssync) {
        if(_splist.ContainsKey(Index) && !_splist[Index].Equals(this)) {
          throw new Exception("Index already used!");
        }
        _splist[Index] = this;
      }
    }

    /// <summary>Returns a SP object that can be used to create SymmetricAlgorithm
    /// and HashAlgorithm objects.</summary>
    public static SecurityPolicy GetPolicy(int Index) {
      return _splist[Index];
    }

    /// <summary>Returns true if the SPI is supported, false otherwise.</summary>
    public static bool Supports(int Index) {
      return _splist.ContainsKey(Index);
    }

    /// <summary>Creates a SymmetricAlgorithm used by this SPI.</summary>
    public SymmetricAlgorithm CreateSymmetricAlgorithm() {
      SymmetricAlgorithm sa = null;

      if(Encryptor.Equals("NullEncryption")) {
        sa = new NullEncryption();
      } else {
        sa = SymmetricAlgorithm.Create(Encryptor);
      }

      if(sa == null) {
        throw new Exception("Unsupported encryption algorithm: " + Encryptor);
      }

      return sa;
    }

    /// <summary>Creates a HashAlgorithm used by this SPI.</summary>
    public HashAlgorithm CreateHashAlgorithm() {
      HashAlgorithm ha = null;

      if(Authenticator.Equals("NullHash")) {
        ha = new NullHash();
      } else {
        ha = HashAlgorithm.Create(Authenticator);
        if(ha == null) {
          ha = KeyedHashAlgorithm.Create(Authenticator);
        }
      }
      
      if(ha == null) {
        throw new Exception("Unsupported authentication method: " + Authenticator);
      }

      return ha;
    }

    public override bool Equals(object o) {
      SecurityPolicy sp = o as SecurityPolicy;
      if(o == null) {
        return false;
      }

      return (sp.Index == this.Index) && sp.Encryptor.Equals(this.Encryptor) &&
        sp.Authenticator.Equals(this.Authenticator);
    }

    public override int GetHashCode() {
      return (Index.GetHashCode() ^
          Encryptor.GetHashCode() ^
          Authenticator.GetHashCode());
    }
  }
}
