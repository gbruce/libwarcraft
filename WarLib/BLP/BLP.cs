﻿using System;
using System.IO;
using System.Drawing;
using System.Collections;
using System.Drawing.Imaging;
using System.Collections.Generic;

using Squish;
using Warcraft.Core;
using Warcraft.Core.ImageQuantization;
using System.Drawing.Drawing2D;

namespace Warcraft.BLP
{
	public class BLP
	{
		public BLPHeader Header;
		private readonly List<Color> Palette = new List<Color>();
		private readonly List<byte[]> RawMipMaps = new List<byte[]>();

		/// <summary>
		/// Initializes a new instance of the <see cref="WarLib.BLP.BLP"/> class.
		/// This constructor reads a binary BLP file from disk.
		/// </summary>
		/// <param name="data">Data.</param>
		public BLP(byte[] data)
		{
			BinaryReader br = new BinaryReader(new MemoryStream(data));

			byte[] fileHeaderBytes = br.ReadBytes(148);
			this.Header = new BLPHeader(fileHeaderBytes);

			if (Header.compressionType == TextureCompressionType.Palettized)
			{
				for (int i = 0; i < 256; ++i)
				{
					byte B = br.ReadByte();
					byte G = br.ReadByte();
					byte R = br.ReadByte();

					// Ignore the alpha. We'll be reading this later.
					byte A = br.ReadByte();
					Color paletteColor = Color.FromArgb(A, R, G, B);
					Palette.Add(paletteColor);
				}
			}
			else
			{
				// Fill up an empty palette - the palette is always present, but we'll be going after offsets anyway
				for (int i = 0; i < 256; ++i)
				{
					Color paletteColor = Color.FromArgb(0, 0, 0, 0);
					Palette.Add(paletteColor);
				}
			}

			// Read the raw mipmap data
			for (int i = 0; i < Header.GetNumMipMaps(); ++i)
			{
				br.BaseStream.Position = Header.mipMapOffsets[i];
				RawMipMaps.Add(br.ReadBytes((int)Header.mipMapSizes[i]));
			}

			br.Close();
			br.Dispose();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WarLib.BLP.BLP"/> class.
		/// This constructor creates a BLP file using the specified compression from a bitmap object.
		/// If the compression type specifed is DXTC, the default pixel format used is DXT1 for opaque textures and DXT3 for the rest.
		/// </summary>
		/// <param name="Image">Image.</param>
		/// <param name="CompressionType">Compression type.</param>
		public BLP(Bitmap Image, TextureCompressionType CompressionType)
		{
			// Set up the header
			this.Header = new BLPHeader();
			Header.compressionType = CompressionType;

			if (CompressionType == TextureCompressionType.Palettized)
			{
				Header.pixelFormat = BLPPixelFormat.Pixel_Palettized;
				// Determine best alpha bit depth
				if (Image.HasAlpha())
				{
					List<byte> alphaLevels = new List<byte>();
					for (int y = 0; y < Image.Height; ++y)
					{
						for (int x = 0; x < Image.Width; ++x)
						{
							Color pixel = Image.GetPixel(x, y);
							if (!alphaLevels.Contains(pixel.A))
							{
								alphaLevels.Add(pixel.A);
							}

							if (alphaLevels.Count > 16)
							{
								break;
							}
						}
					}										

					if (alphaLevels.Count > 16)
					{
						// More than 16? Use a full byte
						Header.alphaBitDepth = 8;
					}
					else if (alphaLevels.Count > 2)
					{
						// More than 2, but less than or equal to 16? Use half a byte
						Header.alphaBitDepth = 4;
					}
					else
					{
						// Just 2? Use a bit instead
						Header.alphaBitDepth = 1;
					}
				}
				else
				{
					// No alpha, so a bit depth of 0.
					Header.alphaBitDepth = 0;
				}
			}
			else if (CompressionType == TextureCompressionType.DXTC)
			{
				Header.alphaBitDepth = 8;

				// Determine best DXTC type (1, 3 or 5)	
				if (Image.HasAlpha())
				{
					// TODO: Differentiate between DXT3 and 5
					Header.pixelFormat = BLPPixelFormat.Pixel_DXT3;
				}
				else
				{
					// DXT1 for no alpha
					Header.pixelFormat = BLPPixelFormat.Pixel_DXT1;
				}
			}
			else if (CompressionType == TextureCompressionType.Uncompressed)
			{
				// The alpha will be stored as a straight ARGB texture, so set it to 8
				Header.alphaBitDepth = 8;
				Header.pixelFormat = BLPPixelFormat.Pixel_A8R8G8B8;
			}

			// What the mip type does is currently unknown, but it's usually set to 1.
			Header.mipMapType = 1;
			Header.resolution = new Resolution((uint)Image.Width, (uint)Image.Height);

			// It's now time to compress the image
			this.RawMipMaps = CompressImage(Image);

			// Calculate the offsets and sizes
			uint mipOffset = (uint)(this.Header.GetSize() + this.Palette.Count * 4);
			foreach (byte[] rawMipMap in this.RawMipMaps)
			{
				uint mipSize = (uint)rawMipMap.Length;

				this.Header.mipMapOffsets.Add(mipOffset);
				this.Header.mipMapSizes.Add(mipSize);

				// Push the offset ahead for the next mipmap
				mipOffset += mipSize;
			}

			// Finally, 
		}

		/// <summary>
		/// Gets a bitmap representing the given zero-based mipmap level.
		/// </summary>
		/// <returns>A bitmap.</returns>
		/// <param name="level">Mipmap level.</param>
		public Bitmap GetMipMap(uint level)
		{			
			return DecompressMipMap(RawMipMaps[(int)level], level);
		}

		/// <summary>
		/// Decompresses a mipmap in the file at the specified level from the specified data.
		/// </summary>
		/// <returns>The mipmap.</returns>
		/// <param name="data">Data containing the mipmap level.</param>
		/// <param name="mipLevel">The mipmap level of the data</param>
		private Bitmap DecompressMipMap(byte[] data, uint MipLevel)
		{
			Bitmap map = null;	
			uint targetXRes = this.GetResolution().X / (uint)Math.Pow(2, MipLevel);
			uint targetYRes = this.GetResolution().Y / (uint)Math.Pow(2, MipLevel);

			if (data.Length > 0 && targetXRes > 0 && targetYRes > 0)
			{
				if (Header.compressionType == TextureCompressionType.Palettized)
				{
					map = new Bitmap((int)targetXRes, (int)targetYRes, PixelFormat.Format32bppArgb);
					BinaryReader br = new BinaryReader(new MemoryStream(data));

					// Read colour information
					for (int y = 0; y < targetYRes; ++y)
					{
						for (int x = 0; x < targetXRes; ++x)
						{
							byte colorIndex = br.ReadByte();
							Color paletteColor = Palette[colorIndex];                           
							map.SetPixel(x, y, paletteColor);
						}
					}

					// Read Alpha information
					List<byte> alphaValues = new List<byte>();
					if (this.GetAlphaBitDepth() > 0)
					{
						if (this.GetAlphaBitDepth() == 1)
						{
							int alphaByteCount = (int)Math.Ceiling(((double)(targetXRes * targetYRes) / 8));
							alphaValues = Decode1BitAlpha(br.ReadBytes(alphaByteCount));
						}
						else if (this.GetAlphaBitDepth() == 4)
						{
							int alphaByteCount = (int)Math.Ceiling(((double)(targetXRes * targetYRes) / 2));
							alphaValues = Decode4BitAlpha(br.ReadBytes(alphaByteCount));
						}
						else if (this.GetAlphaBitDepth() == 8)
						{
							// Directly read the alpha values
							for (int y = 0; y < targetYRes; ++y)
							{
								for (int x = 0; x < targetXRes; ++x)
								{
									byte alphaValue = br.ReadByte();
									alphaValues.Add(alphaValue);
								}
							}					
						}
					}
					else
					{
						// The map is fully opaque
						for (int y = 0; y < targetYRes; ++y)
						{
							for (int x = 0; x < targetXRes; ++x)
							{
								alphaValues.Add(255);
							}
						}
					}

					// Build the final map
					for (int y = 0; y < targetYRes; ++y)
					{
						for (int x = 0; x < targetXRes; ++x)
						{
							int valueIndex = (int)(x + (targetXRes * y));
							byte alphaValue = alphaValues[valueIndex];
							Color pixelColor = map.GetPixel(x, y);
							Color finalPixel = Color.FromArgb(alphaValue, pixelColor.R, pixelColor.G, pixelColor.B);

							map.SetPixel(x, y, finalPixel);
						}
					}

				}
				else if (Header.compressionType == TextureCompressionType.DXTC)
				{     					
					SquishOptions squishOptions = SquishOptions.DXT1;
					if (Header.pixelFormat == BLPPixelFormat.Pixel_DXT3)
					{
						squishOptions = SquishOptions.DXT3;
					}
					else if (Header.pixelFormat == BLPPixelFormat.Pixel_DXT5)
					{
						squishOptions = SquishOptions.DXT5;
					}

					map = (Bitmap)Squish.Squish.DecompressToBitmap(data, (int)targetXRes, (int)targetYRes, squishOptions);
				}
				else if (Header.compressionType == TextureCompressionType.Uncompressed)
				{
					map = new Bitmap((int)targetXRes, (int)targetYRes, PixelFormat.Format32bppArgb);
					BinaryReader br = new BinaryReader(new MemoryStream(data));

					for (int y = 0; y < targetYRes; ++y)
					{
						for (int x = 0; x < targetXRes; ++x)
						{
							byte A = br.ReadByte();
							byte R = br.ReadByte();					
							byte G = br.ReadByte();
							byte B = br.ReadByte();
																
							Color pixelColor = Color.FromArgb(A, R, G, B);
							map.SetPixel(x, y, pixelColor);
						}
					}

					br.Close();
					br.Dispose();
				}
			}		

			return map;
		}

		private Bitmap RGBAToBitmap(byte[] rgba, int width, int height)
		{
			Bitmap map = new Bitmap(width, height, PixelFormat.Format32bppArgb);
			BinaryReader br = new BinaryReader(new MemoryStream(rgba));
			for (int y = 0; y < width; ++y)
			{
				for (int x = 0; x < height; ++x)
				{
					byte R = br.ReadByte();
					byte G = br.ReadByte();					
					byte B = br.ReadByte();
					byte A = br.ReadByte();
																
					Color pixelColor = Color.FromArgb(A, R, G, B);
					map.SetPixel(x, y, pixelColor);
				}
			}

			br.Close();
			br.Dispose();

			return map;
		}

		/// <summary>
		/// Compresses an input bitmap into a list of mipmap using the file's compression settings. 
		/// Mipmap levels which would produce an image with dimensions smaller than 1x1 will return null instead.
		/// The number of mipmaps returned will be <see cref="GetNumReasonableMipLevels"/> + 1. 
		/// </summary>
		/// <returns>The compressed image data.</returns>
		/// <param name="Image">The image to be compressed.</param>
		/// <param name="MipMapLevels">All of the compressed mipmap levels.</param>
		private List<byte[]> CompressImage(Bitmap Image)
		{
			List<byte[]> mipMaps = new List<byte[]>();

			// Generate a palette from the unmipped image for use with the mips
			if (Header.compressionType == TextureCompressionType.Palettized)
			{
				GeneratePalette(Image);
			}

			// Add the original image as the first mipmap
			mipMaps.Add(CompressImage(Image, 0));

			// Then, compress the image N amount of times into mipmaps
			for (int i = 0; i < GetNumReasonableMipMapLevels(); ++i)
			{
				mipMaps.Add(CompressImage(Image, i));
			}

			return mipMaps;
		}

		/// <summary>
		/// Compresses in input bitmap into a single mipmap at the specified mipmap level, where a mip level is a bisection of the resolution.
		/// For instance, a mip level of 2 applied to a 64x64 image would produce an image with a resolution of 16x16.	
		/// This function expects the mipmap level to be reasonable (i.e, not a level which would produce a mip smaller than 1x1)
		/// </summary>
		/// <returns>The image.</returns>
		/// <param name="Image">Image.</param>
		/// <param name="MipLevel">Mip level.</param>
		private byte[] CompressImage(Bitmap Image, int MipLevel)
		{
			// TODO: Stub function
			uint targetXRes = this.GetResolution().X / (uint)Math.Pow(2, MipLevel);
			uint targetYRes = this.GetResolution().Y / (uint)Math.Pow(2, MipLevel);

			Bitmap resizedImage = ResizeImage(Image, (int)targetXRes, (int)targetYRes);
			resizedImage.Save("/home/jarl/Desktop/debug.png");

			List<byte> colourData = new List<byte>();
			List<byte> alphaData = new List<byte>();

			if (Header.compressionType == TextureCompressionType.Palettized)
			{				
				// Generate the colour data
				for (int y = 0; y < targetYRes; ++y)
				{
					for (int x = 0; x < targetXRes; ++x)
					{
						Color nearestColor = FindClosestMatchingColor(resizedImage.GetPixel(x, y));
						byte paletteIndex = (byte)this.Palette.IndexOf(nearestColor);

						colourData.Add(paletteIndex);
					}
				}

				// Generate the alpha data
				if (this.GetAlphaBitDepth() > 0)
				{
					if (this.GetAlphaBitDepth() == 1)
					{
						int alphaByteCount = (int)Math.Ceiling(((double)(targetXRes * targetYRes) / 8));

						// We're going to be attempting to map 8 pixels on each X iteration
						for (int y = 0; y < targetYRes; ++y)
						{
							for (int x = 0; x < targetXRes; x += 8)
							{
								// The alpha value is stored per-bit in the byte (8 alpha values per byte)
								byte alphaByte = 0;

								for (byte i = 0; (i < 8) && (i < targetXRes); ++i)
								{
									byte pixelAlpha = resizedImage.GetPixel(x + i, y).A;
									if (pixelAlpha > 0)
									{
										pixelAlpha = 1;
									}

									// Shift the value into the correct position in the byte
									pixelAlpha = (byte)(pixelAlpha << 7 - i);
									alphaByte = (byte)(alphaByte | pixelAlpha);
								}

								alphaData.Add(alphaByte);
							}
						}
					}
					else if (this.GetAlphaBitDepth() == 4)
					{
						int alphaByteCount = (int)Math.Ceiling(((double)(targetXRes * targetYRes) / 2));

						// We're going to be attempting to map 2 pixels on each X iteration
						for (int y = 0; y < targetYRes; ++y)
						{
							for (int x = 0; x < targetXRes; x += 2)
							{
								// The alpha value is stored as half a byte (2 alpha values per byte)
								// Extract these two values and map them to a byte size (4 bits can hold 0 - 15 alpha)

								byte alphaByte = 0;

								for (byte i = 0; (i < 2) && (i < targetXRes); ++i)
								{
									// Get the value from the image
									byte pixelAlpha = resizedImage.GetPixel(x + i, y).A;
										
									// Map the value to a 4-bit integer
									pixelAlpha = (byte)ExtensionMethods.Map(pixelAlpha, 0, 255, 0, 15);

									// Shift the value to the upper bits on the first iteration, and leave it where it is
									// on the second one
									pixelAlpha = (byte)(pixelAlpha << 4 * (1 - i));

									alphaByte = (byte)(alphaByte | pixelAlpha);
								}

								alphaData.Add(alphaByte);
							}
						}
					}
					else if (this.GetAlphaBitDepth() == 8)
					{
						for (int y = 0; y < targetYRes; ++y)
						{
							for (int x = 0; x < targetXRes; ++x)
							{
								// The alpha value is stored as a whole byte
								byte alphaValue = resizedImage.GetPixel(x, y).A;
								alphaData.Add(alphaValue);
							}
						}					
					}
				}
				else
				{
					// The map is fully opaque
					for (int y = 0; y < targetYRes; ++y)
					{
						for (int x = 0; x < targetXRes; ++x)
						{
							alphaData.Add(255);
						}
					}
				}
			}
			else if (Header.compressionType == TextureCompressionType.DXTC)
			{
				MemoryStream rgbaStream = new MemoryStream();
				BinaryWriter bw = new BinaryWriter(rgbaStream);
				for (int y = 0; y < targetYRes; ++y)
				{
					for (int x = 0; x < targetXRes; ++x)
					{
						bw.Write(resizedImage.GetPixel(x, y).R);
						bw.Write(resizedImage.GetPixel(x, y).G);
						bw.Write(resizedImage.GetPixel(x, y).B);
						bw.Write(resizedImage.GetPixel(x, y).A);
					}
				}		

				// Finish writing the data
				bw.Flush();

				byte[] rgbaBytes = rgbaStream.ToArray();

				bw.Close();
				bw.Dispose();

				SquishOptions squishOptions = SquishOptions.DXT1;
				if (Header.pixelFormat == BLPPixelFormat.Pixel_DXT3)
				{
					squishOptions = SquishOptions.DXT3;
				}
				else if (Header.pixelFormat == BLPPixelFormat.Pixel_DXT5)
				{
					squishOptions = SquishOptions.DXT5;
				}

				// TODO: Implement squish compression
				colourData = new List<byte>(Squish.Squish.CompressImage(rgbaBytes, (int)targetXRes, (int)targetYRes, squishOptions));
			}
			else if (Header.compressionType == TextureCompressionType.Uncompressed)
			{
				MemoryStream argbStream = new MemoryStream();
				BinaryWriter bw = new BinaryWriter(argbStream);
				for (int y = 0; y < targetYRes; ++y)
				{
					for (int x = 0; x < targetXRes; ++x)
					{
						bw.Write(resizedImage.GetPixel(x, y).A);
						bw.Write(resizedImage.GetPixel(x, y).R);
						bw.Write(resizedImage.GetPixel(x, y).G);
						bw.Write(resizedImage.GetPixel(x, y).B);
					}
				}		

				// Finish writing the data
				bw.Flush();

				byte[] argbBytes = argbStream.ToArray();

				bw.Close();
				bw.Dispose();

				colourData = new List<byte>(argbBytes);
			}			

			// After compression of the data, merge the color data and alpha data
			byte[] compressedMipMap = new byte[colourData.Count + alphaData.Count];
			Buffer.BlockCopy(colourData.ToArray(), 0, compressedMipMap, 0, colourData.ToArray().Length);
			Buffer.BlockCopy(alphaData.ToArray(), 0, compressedMipMap, colourData.ToArray().Length, alphaData.ToArray().Length);

			return compressedMipMap;
		}

		/// <summary>
		/// Resize the image to the specified width and height.
		/// Credit goes to https://stackoverflow.com/questions/1922040/resize-an-image-c-sharp (mpen)
		/// </summary>
		/// <param name="image">The image to resize.</param>
		/// <param name="width">The width to resize to.</param>
		/// <param name="height">The height to resize to.</param>
		/// <returns>The resized image.</returns>
		public static Bitmap ResizeImage(Image image, int width, int height)
		{
			var destRect = new Rectangle(0, 0, width, height);
			var destImage = new Bitmap(width, height);

			destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

			using (var graphics = Graphics.FromImage(destImage))
			{
				graphics.CompositingMode = CompositingMode.SourceCopy;
				graphics.CompositingQuality = CompositingQuality.HighQuality;
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics.SmoothingMode = SmoothingMode.HighQuality;
				graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

				using (var wrapMode = new ImageAttributes())
				{
					wrapMode.SetWrapMode(WrapMode.TileFlipXY);
					graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
				}
			}

			return destImage;
		}

		private List<byte> Decode1BitAlpha(byte[] data)
		{
			List<byte> alphaValues = new List<byte>();

			foreach (byte dataByte in data)
			{
				// The alpha value is stored per-bit in the byte (8 alpha values per byte)
				for (byte i = 0; i < 8; ++i)
				{
					byte alphaBit = (byte)ExtensionMethods.Map((byte)((dataByte >> (7 - i)) & 0x01), 0, 1, 0, 255);

					// At this point, alphaBit will be either 0 or 1. Map this to 0 or 255.
					if (alphaBit > 0)
					{
						alphaValues.Add(255);
					}
					else
					{
						alphaValues.Add(0);
					}
				}
			}

			return alphaValues;
		}

		private List<byte> Decode4BitAlpha(byte[] data)
		{
			List<byte> alphaValues = new List<byte>();

			for (int i = 0; i < data.Length; ++i)
			{
				// The alpha value is stored as half a byte (2 alpha values per byte)
				// Extract these two values and map them to a byte size (4 bits can hold 0 - 15 alpha)
				byte alphaByte = data[i];
						
				byte alphaValue1 = (byte)ExtensionMethods.Map((byte)(alphaByte >> 4), 0, 15, 0, 255);
				byte alphaValue2 = (byte)ExtensionMethods.Map((byte)(alphaByte & 0x0F), 0, 15, 0, 255);

				alphaValues.Add(alphaValue1);
				alphaValues.Add(alphaValue2);
			}

			return alphaValues;
		}

		private List<byte> Encode1BitAlpha(Bitmap map)
		{
			return null;
		}

		private List<byte> Encode4BitAlpha(Bitmap map)
		{
			return null;
		}

		/// <summary>
		/// Gets the number of mipmaps which can be produced in this file without producing a mipmap smaller than 1x1.
		/// </summary>
		/// <returns>The number of reasonable mip map levels.</returns>
		private uint GetNumReasonableMipMapLevels()
		{
			uint smallestXRes = this.GetResolution().X;
			uint smallestYRes = this.GetResolution().Y;

			uint mipLevels = 0;
			while (smallestXRes > 1 && smallestYRes > 1)
			{
				// Bisect the resolution using the current number of mip levels.
				smallestXRes = smallestXRes / (uint)Math.Pow(2, mipLevels);
				smallestYRes = smallestYRes / (uint)Math.Pow(2, mipLevels);

				++mipLevels;
			}

			return mipLevels.Clamp<uint>(0, 15);
		}

		/// <summary>
		/// Generates an indexed 256-color palette from the specified image and overwrites the current palette with it.
		/// Ordinarily, this would be the original mipmap.
		/// </summary>
		/// <param name="Image">Image.</param>
		private void GeneratePalette(Bitmap Image)
		{
			// TODO: Replace with an algorithm that produces a better result. For now, it works.
			PaletteQuantizer quantizer = new PaletteQuantizer(new ArrayList());
			Bitmap quantizedMap = quantizer.Quantize(Image);
			this.Palette.Clear();
			this.Palette.AddRange(quantizedMap.Palette.Entries);

		}

		/// <summary>
		/// Finds the closest matching color in the palette for the given input color.
		/// </summary>
		/// <returns>The closest matching color.</returns>
		/// <param name="InColor">Input color.</param>
		private Color FindClosestMatchingColor(Color InColor)
		{
			Color NearestColor = Color.Empty;

			// Drop out if the palette contains an exact match
			if (Palette.Contains(InColor))
			{
				return InColor;
			}

			double ColorDistance = 250000.0;
			foreach (Color PaletteColor in Palette)
			{				
				double TestRed = Math.Pow(Convert.ToDouble(PaletteColor.R) - InColor.R, 2.0);
				double TestGreen = Math.Pow(Convert.ToDouble(PaletteColor.G) - InColor.G, 2.0);
				double TestBlue = Math.Pow(Convert.ToDouble(PaletteColor.B) - InColor.B, 2.0);

				double DistanceResult = Math.Sqrt(TestBlue + TestGreen + TestRed);			

				if (DistanceResult == 0.0)
				{
					NearestColor = PaletteColor;
					break;
				}
				else if (DistanceResult < ColorDistance)
				{
					ColorDistance = DistanceResult;
					NearestColor = PaletteColor;
				}
			}

			return NearestColor;
		}

		/// <summary>
		/// Gets the raw bytes of the palette (or an array with length 0 if there isn't a palette)
		/// </summary>
		/// <returns>The palette bytes.</returns>
		private byte[] GetPaletteBytes()
		{
			List<byte> bytes = new List<byte>();
			foreach (Color color in Palette)
			{
				bytes.Add(color.B);
				bytes.Add(color.G);
				bytes.Add(color.R);
				bytes.Add(color.A);
			}

			return bytes.ToArray();
		}

		/// <summary>
		/// Gets the raw, BLP-encoded mipmaps as a byte array for writing to disk.
		/// </summary>
		/// <returns>The mip map bytes.</returns>
		private byte[] GetMipMapBytes()
		{
			List<byte> mipmapBytes = new List<byte>();
			foreach (byte[] mipmap in RawMipMaps)
			{
				foreach (byte mipbyte in mipmap)
				{
					mipmapBytes.Add(mipbyte);
				}
			}

			return mipmapBytes.ToArray();
		}

		/// <summary>
		/// Gets the BLP image object as a byte array, which can be written to disk as a file.
		/// </summary>
		/// <returns>The bytes.</returns>
		public byte[] GetBytes()
		{
			byte[] headerBytes = this.Header.GetBytes();
			byte[] paletteBytes = GetPaletteBytes();
			byte[] mipBytes = GetMipMapBytes();

			byte[] imageBytes = new byte[headerBytes.Length + paletteBytes.Length + mipBytes.Length];

			Buffer.BlockCopy(headerBytes, 0, imageBytes, 0, headerBytes.Length);
			Buffer.BlockCopy(paletteBytes, 0, imageBytes, headerBytes.Length, paletteBytes.Length);
			Buffer.BlockCopy(mipBytes, 0, imageBytes, headerBytes.Length + paletteBytes.Length, mipBytes.Length);

			return imageBytes;
		}

		/// <summary>
		/// Gets the best mip map for the specified resolution, where the specified resolution
		/// is the maximum resolution for any dimension in the image.
		/// </summary>
		/// <returns>The best mip map.</returns>
		/// <param name="MaxResolution">Max resolution.</param>
		public Bitmap GetBestMipMap(uint MaxResolution)
		{
			// Calulcate the best mip level
			double XMip = Math.Ceiling((double)GetResolution().X / MaxResolution) - 1;
			double YMip = Math.Ceiling((double)GetResolution().Y / MaxResolution) - 1;

			if (XMip > YMip)
			{
				// Grab the mipmap based on the X Mip
				return GetMipMap((uint)XMip);
			}
			else if (YMip > XMip)
			{
				// Grab the mipmap based on the Y Mip
				return GetMipMap((uint)YMip);
			}
			else
			{
				// Doesn't matter which one, just grab the X Mip
				return GetMipMap((uint)XMip);
			}
		}

		/// <summary>
		/// Writes the image to disk as a BLP file.
		/// To write a "normal" image format to disk, retrieve a mipmap (<see cref="WarLib.BLP.BLP.GetMipMap"/>) instead.
		/// </summary>
		/// <param name="path">Path.</param>
		private void WriteImageToDisk(string path)
		{
			File.WriteAllBytes(path, GetBytes());
		}

		/// <summary>
		/// Gets the magic string that identifies this file.
		/// </summary>
		/// <returns>The magic string.</returns>
		public string GetFileType()
		{
			return Header.fileType;
		}

		/// <summary>
		/// Gets the version of the BLP file.
		/// </summary>
		/// <returns>The version of the file.</returns>
		public uint GetVersion()
		{
			return Header.version;
		}

		/// <summary>
		/// Gets the BLP pixel format. This format represents a subtype of the compression used in the file.
		/// </summary>
		/// <returns>The pixel format.</returns>
		public BLPPixelFormat GetPixelFormat()
		{
			return Header.pixelFormat;
		}

		/// <summary>
		/// Gets the resolution of the image.
		/// </summary>
		/// <returns>The resolution.</returns>
		public Resolution GetResolution()
		{
			return Header.resolution;
		}

		/// <summary>
		/// Gets the type of compression used in the image.
		/// </summary>
		/// <returns>The compression type.</returns>
		public TextureCompressionType GetCompressionType()
		{
			return Header.compressionType;
		}

		/// <summary>
		/// Gets the alpha bit depth. This value represents where the alpha value for each pixel is stored.
		/// </summary>
		/// <returns>The alpha bit depth.</returns>
		public int GetAlphaBitDepth()
		{
			return Header.alphaBitDepth;
		}

		/// <summary>
		/// Gets the number of mipmap levels in the image.
		/// </summary>
		/// <returns>The mipmap count.</returns>
		public int GetMipMapCount()
		{
			return RawMipMaps.Count;
		}
	}
}
