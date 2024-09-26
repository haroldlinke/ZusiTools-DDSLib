using System;
using System.IO;

namespace DDSLib.Pfim
{
#pragma warning disable IDE0079

    /// <summary>
    /// Provides a mechanism for decoding and storing the decoded information
    /// about a targa image
    /// </summary>
    public class Targa : IImage
    {
        private readonly PfimConfig _config;

        /// <summary>
        /// Constructs a targa image from a targa image and raw data
        /// </summary>
        private Targa(TargaHeader header, PfimConfig config, byte[] data, int dataLen)
        {
            _config = config;
            Header = header;
            Data = data;
            DataLen = dataLen;
        }

        public static Targa Create(byte[] data, PfimConfig config)
        {
            return Create(Util.CreateExposed(data), config);
        }

        public bool Compressed => false;
        public void Decompress()
        {
            // Never compressed
        }

        /// <summary>
        /// Creates a targa image from a given stream. The type of targa is determined from the
        /// targa header, which is assumed to be a part of the stream
        /// </summary>
        /// <param name="str">Stream to read the targa image from</param>
        /// <returns>A targa image</returns>
        public static Targa Create(Stream str, PfimConfig config)
        {
            var header = new TargaHeader(str, config);
            return DecodeTarga(str, config, header);
        }

        internal static IImage CreateWithPartialHeader(Stream str, PfimConfig config, byte[] magic)
        {
            var header = new TargaHeader(str, magic, 4, config);
            return DecodeTarga(str, config, header);
        }

        private static Targa DecodeTarga(Stream str, PfimConfig config, TargaHeader header)
        {
            var targa = (header.IsCompressed)
                ? (IDecodeTarga) (new CompressedTarga())
                : new UncompressedTarga();
            byte[] data = header.Orientation switch
            {
                TargaHeader.TargaOrientation.BottomLeft => targa.BottomLeft(str, header, config),
                TargaHeader.TargaOrientation.BottomRight => targa.BottomRight(str, header, config),
                TargaHeader.TargaOrientation.TopRight => targa.TopRight(str, header, config),
                TargaHeader.TargaOrientation.TopLeft => targa.TopLeft(str, header, config),
                _ => throw new Exception("Targa orientation not recognized"),
            };
            var stride = Util.Stride(header.Width, header.PixelDepthBits);
            var len = header.Height * stride;
            var result = new Targa(header, config, data, len);

            if (config.ApplyColorMap)
            {
                result.ApplyColorMap();
            }

            return result;
        }

        public void ApplyColorMap()
        {
            // Check targa header field 2 and 3 as "it is best to check Field 3, Image Type, 
            // to make sure you have a file which can use the data stored in the Color Map Field.
            // Otherwise ignore the information"
            if (!Header.HasColorMap || 
                (Header.ImageType != TargaHeader.TargaImageType.RunLengthColorMap &&
                Header.ImageType != TargaHeader.TargaImageType.UncompressedColorMap)) {
                return;
            }

            var colorMapDepthBytes = Header.ColorMapDepthBytes;
            var oldStride = Stride;
            var newStride = Util.Stride(Header.Width, colorMapDepthBytes * 8);
            var newLen = colorMapDepthBytes * DataLen;
            var newData = _config.Allocator.Rent(newLen);
            switch (Header.ColorMapDepthBits)
            {
                case 16:
                case 24:
                case 32:
                    for (int i = 0; i < Header.Height; i++)
                    {
                        var dataOffset = i * oldStride;
                        var newDataOffset = i * newStride;
                        for (int j = 0; j < Header.Width; j++)
                        {
                            var colorMapIndex = Data[dataOffset + j] * colorMapDepthBytes;
                            for (int k = 0; k < colorMapDepthBytes; k++)
                            {
                                newData[newDataOffset + (j * colorMapDepthBytes) + k] = Header.ColorMap[colorMapIndex + k];
                            }
                        }
                    }
                    break;
                default:
                    throw new NotImplementedException($"Unrecognized color map depth {Header.ColorMapDepthBits}");
            }

            _config.Allocator.Return(Data);
            Data = newData;
            DataLen = newLen;
            Header.PixelDepthBits = (byte)Header.ColorMapDepthBits;
            Header.ColorMap = Array.Empty<byte>();
            Header.ColorMapLength = 0;
            Header.HasColorMap = false;
            Header.ColorMapDepthBits = 0;
        }

        public MipMapOffset[] MipMaps => Array.Empty<MipMapOffset>();

        /// <summary>The raw image data</summary>
        public byte[] Data { get; private set; }

        public int DataLen { get; private set; }

        public TargaHeader Header { get; private set; }

        /// <summary>Width of the image in pixels</summary>
        public int Width => Header.Width;

        /// <summary>Height of the image in pixels</summary>
        public int Height => Header.Height;

        /// <summary>The number of bytes that compose one line</summary>
        public int Stride => Util.Stride(Header.Width, Header.PixelDepthBits);

        public int BitsPerPixel => Header.PixelDepthBits;

        /// <summary>The format of the raw data</summary>
        public ImageFormat Format
        {
            get
            {
                return Header.PixelDepthBits switch
                {
                    8 => ImageFormat.Rgb8,
                    16 => ImageFormat.R5g5b5,
                    24 => ImageFormat.Rgb24,
                    32 => ImageFormat.Rgba32,
                    _ => throw new Exception($"Unrecognized pixel depth: {Header.PixelDepthBits}"),
                };
            }
        }

#pragma warning disable CA1816
        public void Dispose()
        {
            _config.Allocator.Return(Data);
        }
    }
}
