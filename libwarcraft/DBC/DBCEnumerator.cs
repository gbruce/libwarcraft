//
//  DBCEnumerator.cs
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

using System.Collections;
using System.Collections.Generic;
using System.IO;
using Warcraft.DBC.Definitions;
using Warcraft.Core.Extensions;

namespace Warcraft.DBC
{
	/// <summary>
	/// Enumerator object of a DBC object.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class DBCEnumerator<T> : IEnumerator<T> where T : DBCRecord, new()
	{
		private readonly DBC<T> ParentDatabase;
		private readonly BinaryReader DatabaseReader;
		private readonly long StringBlockOffset;

		/// <summary>
		/// Initialize a new <see cref="DBCEnumerator{T}"/> from a given database, its data, and where the string block
		/// begins in it.
		/// </summary>
		/// <param name="database"></param>
		/// <param name="data"></param>
		/// <param name="stringBlockOffset"></param>
		public DBCEnumerator(DBC<T> database, byte[] data, long stringBlockOffset)
		{
			this.ParentDatabase = database;
			this.StringBlockOffset = stringBlockOffset;
			this.DatabaseReader = new BinaryReader(new MemoryStream(data));

			// Seek to the start of the record block
			this.DatabaseReader.BaseStream.Seek(DBCHeader.GetSize(), SeekOrigin.Begin);
		}

		/// <summary>
		/// Reads the record at the current position, and moves the enumerator to the next record.
		/// </summary>
		/// <returns></returns>
		public bool MoveNext()
		{
			long recordBlockEnd = this.StringBlockOffset;
			if (this.DatabaseReader.BaseStream.Position >= recordBlockEnd)
			{
				return false;
			}

			this.Current = this.DatabaseReader.ReadRecord<T>(this.ParentDatabase.FieldCount, this.ParentDatabase.RecordSize, this.ParentDatabase.Version);


			return this.DatabaseReader.BaseStream.Position != recordBlockEnd;
		}

		/// <summary>
		/// Resets the stream back to the first record.
		/// </summary>
		public void Reset()
		{
			this.DatabaseReader.BaseStream.Seek(DBCHeader.GetSize(), SeekOrigin.Begin);
			this.Current = null;
		}

		/// <summary>
		/// Gets the current record.
		/// </summary>
		public T Current
		{
			get
			{
				if (this.CurrentInternal == null)
				{
					return null;
				}

				foreach (var stringReference in this.CurrentInternal.GetStringReferences())
				{
					this.ParentDatabase.ResolveStringReference(stringReference);
				}
				return this.CurrentInternal;
			}
			private set => this.CurrentInternal = value;
		}

		private T CurrentInternal;

		object IEnumerator.Current => this.Current;

		/// <summary>
		/// Disposes the database and any underlying streams.
		/// </summary>
		public void Dispose()
		{
			this.DatabaseReader.Dispose();
		}
	}
}