﻿//
//  CreatureDisplayInfoRecord.cs
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
using Warcraft.Core.Extensions;
using Warcraft.DBC.SpecialFields;

namespace Warcraft.DBC.Definitions
{
	public class CreatureDisplayInfoRecord : DBCRecord
	{
		public const DatabaseName Database = DatabaseName.CreatureDisplayInfo;

		public ForeignKey<uint> Model;
		public ForeignKey<uint> Sound;
		public ForeignKey<uint> ExtraDisplayInformation;
		public float Scale;
		public uint Opacity;
		public List<StringReference> TextureVariations;

		public StringReference PortraitTexture;

		public uint SizeClass;

		public ForeignKey<uint> BloodLevel;

		public ForeignKey<uint> Blood;
		public ForeignKey<uint> NPCSound;

		public uint ParticleColour;

		public uint CreatureGeosetData;
		public uint ObjectEffectPackage;

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

			this.Model = new ForeignKey<uint>(DatabaseName.CreatureModelData, nameof(DBCRecord.ID), reader.ReadUInt32());
			this.Sound = new ForeignKey<uint>(DatabaseName.CreatureSoundData, nameof(DBCRecord.ID), reader.ReadUInt32());
			this.ExtraDisplayInformation = new ForeignKey<uint>(DatabaseName.CreatureDisplayInfoExtra, nameof(DBCRecord.ID), reader.ReadUInt32());
			this.Scale = reader.ReadSingle();
			this.Opacity = reader.ReadUInt32();

			// There are always three texture references, but any one of them may point to an empty string.
			this.TextureVariations = new List<StringReference>
			{
				reader.ReadStringReference(),
				reader.ReadStringReference(),
				reader.ReadStringReference()
			};

			if (this.Version >= WarcraftVersion.BurningCrusade)
			{
				this.PortraitTexture = reader.ReadStringReference();
			}

			if (this.Version >= WarcraftVersion.Wrath)
			{
				this.BloodLevel = new ForeignKey<uint>(DatabaseName.UnitBloodLevels, nameof(DBCRecord.ID), reader.ReadUInt32());
			}
			else
			{
				this.SizeClass = reader.ReadUInt32();
			}

			this.Blood = new ForeignKey<uint>(DatabaseName.UnitBlood, nameof(DBCRecord.ID), reader.ReadUInt32());
			this.NPCSound = new ForeignKey<uint>(DatabaseName.NPCSounds, nameof(DBCRecord.ID), reader.ReadUInt32());

			if (this.Version >= WarcraftVersion.BurningCrusade)
			{
				this.ParticleColour = reader.ReadUInt32();
			}

			if (this.Version >= WarcraftVersion.Wrath)
			{
				this.CreatureGeosetData = reader.ReadUInt32();
				this.ObjectEffectPackage = reader.ReadUInt32();
			}

			this.HasLoadedRecordData = true;
		}

		public override IEnumerable<StringReference> GetStringReferences()
		{
			return this.TextureVariations;
		}

		public override int FieldCount
		{
			get
			{
				switch (this.Version)
				{
					case WarcraftVersion.Classic: return 12;
					case WarcraftVersion.BurningCrusade: return 14;
					case WarcraftVersion.Wrath: return 16;
					default: throw new NotImplementedException();
				}
			}
		}

		public override int RecordSize => sizeof(uint) * this.FieldCount;
	}
}