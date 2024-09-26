using DDSLib.Pfim.dds;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DDSLib.Pfim
{
    public class DdsHeaderDxt10
    {
        public DxgiFormat DxgiFormat { get; }
        public D3D10ResourceDimension ResourceDimension { get; }
        public uint MiscFlag { get; }
        public uint ArraySize { get; }
        public uint MiscFlags2 { get; }

        public unsafe DdsHeaderDxt10(Stream stream)
        {
            byte[] buffer = new byte[5 * 4];
            Util.ReadExactly(stream, buffer, 0, buffer.Length);

            fixed (byte* bufferPtr = buffer)
            {
                uint* ptr = (uint*) bufferPtr;

                DxgiFormat = (DxgiFormat) (*ptr++);
                ResourceDimension = (D3D10ResourceDimension) (*ptr++);
                MiscFlag = (*ptr++);
                ArraySize = (*ptr++);
                MiscFlags2 = (*ptr);
            }
        }

        internal Dds NewDecoder(DdsHeader header, PfimConfig config)
        {
            return DxgiFormat switch
            {
                DxgiFormat.BC1_TYPELESS or DxgiFormat.BC1_UNORM_SRGB or DxgiFormat.BC1_UNORM => new Dxt1Dds(header, config),
                DxgiFormat.BC2_TYPELESS or DxgiFormat.BC2_UNORM or DxgiFormat.BC2_UNORM_SRGB => new Dxt3Dds(header, config),
                DxgiFormat.BC3_TYPELESS or DxgiFormat.BC3_UNORM or DxgiFormat.BC3_UNORM_SRGB => new Dxt5Dds(header, config),
                DxgiFormat.BC4_TYPELESS or DxgiFormat.BC4_UNORM => new Bc4Dds(header, config),
                DxgiFormat.BC4_SNORM => new Bc4sDds(header, config),
                DxgiFormat.BC5_TYPELESS or DxgiFormat.BC5_UNORM => new Bc5Dds(header, config),
                DxgiFormat.BC5_SNORM => new Bc5sDds(header, config),
                DxgiFormat.BC6H_TYPELESS or DxgiFormat.BC6H_UF16 or DxgiFormat.BC6H_SF16 => new Bc6hDds(header, config),
                DxgiFormat.BC7_TYPELESS or DxgiFormat.BC7_UNORM or DxgiFormat.BC7_UNORM_SRGB => new Bc7Dds(header, config),
                DxgiFormat.R8G8B8A8_TYPELESS or DxgiFormat.R8G8B8A8_UNORM or DxgiFormat.R8G8B8A8_UNORM_SRGB or DxgiFormat.R8G8B8A8_UINT or DxgiFormat.R8G8B8A8_SNORM or DxgiFormat.R8G8B8A8_SINT => new UncompressedDds(header, config, 32, true),
                DxgiFormat.B8G8R8A8_TYPELESS or DxgiFormat.B8G8R8A8_UNORM or DxgiFormat.B8G8R8A8_UNORM_SRGB or DxgiFormat.B8G8R8X8_UNORM_SRGB => new UncompressedDds(header, config, 32, false),
                DxgiFormat.B5G5R5A1_UNORM => new UncompressedDds(header, config, 16, false),
                _ => throw new ArgumentException($"Unimplemented DXGI format: {DxgiFormat}"),
            };
        }
    }
}
