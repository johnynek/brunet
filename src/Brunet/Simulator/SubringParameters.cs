/*
This program is part of Brunet, a library for autonomic overlay networks.
Copyright (C) 2010 David Wolinsky davidiw@ufl.edu, Unversity of Florida

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

namespace Brunet.Simulator {
  /// <summary>Parameters for subrings.</summary>
  public class SubringParameters : Parameters {
    /// <summary>Parameters used to create the Private Overlay.</summary>
    public Parameters PrivateParameters { get { return _private_parameters; } }
    /// <summary>Parameters used to create the Public Overlay.</summary>
    public Parameters PublicParameters { get { return _public_parameters; } }
    public bool Subring { get { return _subring; } }

    protected Parameters _private_parameters;
    protected Parameters _public_parameters;
    protected bool _subring = false;
    protected int _subring_count = 0;

    public SubringParameters() :
      base("SubringSimulator", "Simulator for Subrings")
    {
      _options.Add("subring=", "Enable subrings by specifying node count",
          v => _subring_count = Int32.Parse(v));
    }

    public SubringParameters(SubringParameters parameters) : base(parameters)
    {
    }

    override public int Parse(string[] args)
    {
      if(base.Parse(args) < 0) {
        return -1;
      }

      if(_subring_count > 0) {
        _subring = true;
      }

      SubringParameters prip = new SubringParameters(this);
      prip._size = _subring_count;
      prip._pathing = false;
      _private_parameters = prip;

      SubringParameters pubp = new SubringParameters(this);
      pubp._pathing = true;
      pubp._dtls = false;
      pubp._se = false;
      pubp._ss = false;
      _public_parameters = pubp;

      return 0;
    }
  }
}
