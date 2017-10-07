﻿//
//  TerrainMapChunk.cs
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

using System.IO;
using Warcraft.ADT.Chunks.Subchunks;
using Warcraft.Core.Extensions;
using Warcraft.Core.Interfaces;

namespace Warcraft.ADT.Chunks
{
	/// <summary>
	/// MCNK Chunk - Main map chunk which contains a number of smaller subchunks. 256 of these are present in an ADT file.
	/// </summary>
	public class TerrainMapChunk : IIFFChunk
	{
		public const string Signature = "MCNK";
		/// <summary>
		/// Header contains information about the MCNK and its subchunks such as offsets, position and flags.
		/// </summary>
		public MapChunkHeader Header;

		/// <summary>
		/// Heightmap Chunk
		/// </summary>
		public MapChunkHeightmap Heightmap;

		/// <summary>
		/// Normal map chunk
		/// </summary>
		public MapChunkVertexNormals VertexNormals;

		/// <summary>
		/// Alphamap Layer chunk
		/// </summary>
		public MapChunkTextureLayers TextureLayers;

		/// <summary>
		/// Map Object References chunk
		/// </summary>
		public MapChunkModelReferences ModelReferences;

		/// <summary>
		/// Alphamap chunk
		/// </summary>
		public MapChunkAlphaMaps AlphaMaps;

		/// <summary>
		/// The baked shadows.
		/// </summary>
		public MapChunkBakedShadows BakedShadows;

		/// <summary>
		/// Sound Emitter Chunk
		/// </summary>
		public MapChunkSoundEmitters SoundEmitters;

		/// <summary>
		/// Liquid Chunk
		/// </summary>
		public MapChunkLiquids Liquid;

		/// <summary>
		/// The vertex shading chunk.
		/// </summary>
		public MapChunkVertexShading VertexShading;

		/// <summary>
		/// The vertex lighting chunk
		/// </summary>
		public MapChunkVertexLighting VertexLighting;

		public TerrainMapChunk()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Warcraft.ADT.Chunks.TerrainMapChunk"/> class.
		/// </summary>
		/// <param name="inData">ExtendedData.</param>
		public TerrainMapChunk(byte[] inData)
		{
			LoadBinaryData(inData);
		}

		public void LoadBinaryData(byte[] inData)
        {
			using (MemoryStream ms = new MemoryStream(inData))
			{
				using (BinaryReader br = new BinaryReader(ms))
				{
          this.Header = new MapChunkHeader(br.ReadBytes(MapChunkHeader.GetSize()));

					if (this.Header.HeightmapOffset > 0)
					{
            br.BaseStream.Position = this.Header.HeightmapOffset - 8;
						this.Heightmap = br.ReadIFFChunk<MapChunkHeightmap>();
					}

					if (this.Header.VertexNormalOffset > 0)
					{
						br.BaseStream.Position = this.Header.VertexNormalOffset - 8;
						this.VertexNormals = br.ReadIFFChunk<MapChunkVertexNormals>();
					}

					if (this.Header.TextureLayersOffset > 0)
					{
						br.BaseStream.Position = this.Header.TextureLayersOffset - 8;
						this.TextureLayers = br.ReadIFFChunk<MapChunkTextureLayers>();
					}

					if (this.Header.ModelReferencesOffset > 0)
					{
						br.BaseStream.Position = this.Header.ModelReferencesOffset - 8;
						this.ModelReferences = br.ReadIFFChunk<MapChunkModelReferences>();

            // FIXME: figure this out
            // this.ModelReferences.PostLoadReferences(this.Header.ModelReferenceCount, this.Header.WorldModelObjectReferenceCount);
					}

					if (this.Header.AlphaMapsOffset > 0)
					{
						br.BaseStream.Position = this.Header.AlphaMapsOffset - 8;
						this.AlphaMaps = br.ReadIFFChunk<MapChunkAlphaMaps>();
					}

					if (this.Header.BakedShadowsOffset > 0 && this.Header.Flags.HasFlag(MapChunkFlags.HasBakedShadows))
					{
						br.BaseStream.Position = this.Header.BakedShadowsOffset - 8;
						this.BakedShadows = br.ReadIFFChunk<MapChunkBakedShadows>();
					}

					if (this.Header.SoundEmittersOffset > 0 && this.Header.SoundEmitterCount > 0)
					{
						br.BaseStream.Position = this.Header.SoundEmittersOffset - 8;
						this.SoundEmitters = br.ReadIFFChunk<MapChunkSoundEmitters>();
					}

					if (this.Header.LiquidOffset > 0 && this.Header.LiquidSize > 8)
					{
						br.BaseStream.Position = this.Header.LiquidOffset - 8;
						this.Liquid = br.ReadIFFChunk<MapChunkLiquids>();
					}

					if (this.Header.VertexShadingOffset > 0 && this.Header.Flags.HasFlag(MapChunkFlags.HasVertexShading))
					{
						br.BaseStream.Position = this.Header.SoundEmittersOffset - 8;
						this.VertexShading = br.ReadIFFChunk<MapChunkVertexShading>();
					}

					if (this.Header.VertexLightingOffset > 0)
					{
						br.BaseStream.Position = this.Header.VertexLightingOffset - 8;
						this.VertexLighting = br.ReadIFFChunk<MapChunkVertexLighting>();
					}
				}
			}
        }

        public string GetSignature()
        {
        	return Signature;
        }
	}
}

