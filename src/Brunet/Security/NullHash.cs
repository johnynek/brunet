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
using System.Security.Cryptography;

#if BRUNET_NUNIT
using NUnit.Framework;
#endif

namespace Brunet.Security {
  /// <summary>Provides a Null HashAlgorithm, its blazing fast!  Typically,
  /// these classes  are not thread-safe, but the block-size is 1, there
  /// is no history, and the result is always an empty byte array.</summary>
  public class NullHash : HashAlgorithm {
    public NullHash()
    {
      HashSizeValue = 0;
    }

    protected override void HashCore(byte[] rgb, int start, int size)
    {
    }

    protected override byte[] HashFinal()
    {
      return new byte[0];
    }

    public override void Initialize()
    {
    }
  }
#if BRUNET_NUNIT
#endif
}

