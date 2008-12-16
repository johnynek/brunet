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
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Brunet {
  /// <summary>Each SP supports a specific SymmetricAlgorithm and HashAlgorithm.</summary>
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
      new SecurityPolicy(DefaultSPI, DefaultEncryptor, DefaultAuthenticator,
          DefaultPreExchangedKeys);
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
