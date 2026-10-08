using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ViVeUI.Windows;
internal static class IconResources
{
    public static ImageSource Load() => BitmapFrame.Create(new Uri("pack://application:,,,/ViVeUI;component/Assets/AppIcon.ico"));
    public static object Validate(Window window)
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/ViVeUI;component/Assets/AppIcon.ico")).Stream;
        using var bytes = new MemoryStream();stream.CopyTo(bytes);var data=bytes.ToArray();
        if(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2))!=1)throw new Exception("Embedded icon has invalid type.");
        var count=BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4));var sizes=new List<int>();
        for(var i=0;i<count;i++)
        {
            var entry=6+i*16;var size=data[entry]==0?256:data[entry];sizes.Add(size);
            var length=BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(entry+8));var offset=BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(entry+12));
            if(offset<6+count*16 || length<24 || offset+length>data.Length || !data.AsSpan(offset,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))throw new Exception("Invalid icon frame.");
            if(BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset+16))!=size || BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset+20))!=size)throw new Exception("Icon frame dimensions differ.");
        }
        if(!sizes.SequenceEqual(new[]{16,20,24,32,40,48,64,128,256}) || window.Icon is null)throw new Exception("Missing high-DPI icon frames or window icon.");
        var executable=Environment.ProcessPath!;var groups=ExtractIconEx(executable,-1,null,null,0);
        if(groups<1)throw new Exception("Running executable has no extractable shell icon.");
        return new { sizes, executableIconGroups=groups, windowIcon=true, sameEmbeddedSource=true };
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern uint ExtractIconEx(string path,int index,IntPtr[]? large,IntPtr[]? small,uint count);
}
