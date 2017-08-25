//
//  SoundProviderPreferences.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2016 Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Collections.Generic;
using System.IO;
using Warcraft.Core;
using Warcraft.DBC.SpecialFields;

namespace Warcraft.DBC.Definitions
{
	public class SoundProviderPreferencesRecord : DBCRecord
	{
		public const DatabaseName Database = DatabaseName.SoundProviderPreferences;

		public StringReference Description;
		public uint Flags;
		public uint EAXEnvironmentSelection;
		public float EAXEffectVolume;
		public float EAXDecayTime;
		public float EAXDamping;
		public float EAX2EnvironmentSize;
		public float EAX2EvironmentDiffusion;
		public uint EAX2Room;
		public uint EAX2RoomHF;
		public float EAX2DecayHFRatio;
		public uint EAX2Reflections;
		public float EAX2ReflectionsDelay;
		public uint EAX2Reverb;
		public float EAX2ReverbDelay;
		public float EAX2RoomRolloff;
		public float EAX2AirAbsorption;
		public uint EAX3RoomLF;
		public float EAX3DecayLFRatio;
		public float EAX3EchoTime;
		public float EAX3EchoDepth;
		public float EAX3ModulationTime;
		public float EAX3ModulationDepth;
		public float EAX3HFReference;
		public float EAX3LFReference;

		/// <summary>
		/// Loads and parses the provided data.
		/// </summary>
		/// <param name="data">ExtendedData.</param>
		public override void PostLoad(byte[] data)
		{
			using (MemoryStream ms = new MemoryStream(data))
			{
				using (BinaryReader br = new BinaryReader(ms))
				{
					DeserializeSelf(br);
				}
			}
		}

		/// <summary>
		/// Deserializes the data of the object using the provided <see cref="BinaryReader"/>.
		/// </summary>
		/// <param name="reader"></param>
		public override void DeserializeSelf(BinaryReader reader)
		{
			base.DeserializeSelf(reader);

			throw new NotImplementedException();
			this.HasLoadedRecordData = true;
		}

		public override IEnumerable<StringReference> GetStringReferences()
		{
			yield return this.Description;
		}

		public override int FieldCount => throw new System.NotImplementedException();

		public override int RecordSize => throw new System.NotImplementedException();
	}
}