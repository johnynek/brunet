/*
Copyright (C) 2009  Kyungyong Lee and David Wolinsky <davidiw@ufl.edu>, University of Florida

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
using System.Xml;
using System.Xml.Schema;
using System.Reflection;

namespace Brunet.Applications {
  public class ConfigurationValidator
  {
    protected bool _failed;
    protected string _message;

    ///<summary> Read xml file and check validity using given xsd file.
    /// brunet and ipop configuration file input type is file name, and
    /// dhcp configuration file content is passed as a string.</summary>
    public bool Validate(string config_path, string xsd_path)
    {
      _failed = false;

      Assembly assem = Assembly.GetExecutingAssembly();
      Stream schema_stream = assem.GetManifestResourceStream(xsd_path);
      XmlSchema test_schema = XmlSchema.Read(schema_stream, null);

      XmlReaderSettings settings = new XmlReaderSettings();
      settings.ValidationType = ValidationType.Schema;
      XmlSchemaSet schemas = new XmlSchemaSet();
      settings.Schemas = schemas;
      schemas.Add(test_schema);
      settings.ValidationEventHandler += ValidEventHandler;
      XmlReader validator = XmlReader.Create(config_path, settings);

      while(validator.Read() && !_failed);
      validator.Close();

      if(_failed) {
        throw new Exception(_message);
      }

      return true;
    }

    protected void ValidEventHandler(object sender, ValidationEventArgs args) {
      _failed = true;
      _message = args.Message;
    }
  }
}
