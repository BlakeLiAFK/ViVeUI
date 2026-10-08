import AppKit
import Foundation
let input = CommandLine.arguments[1], output = CommandLine.arguments[2]
let original = NSImage(contentsOfFile: input)!
let sizes = [16,20,24,32,40,48,64,128,256]
var frames = [Data]()
for size in sizes {
    let bitmap = NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:size,pixelsHigh:size,bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,bytesPerRow:0,bitsPerPixel:0)!
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep:bitmap)
    NSGraphicsContext.current!.imageInterpolation = .high
    original.draw(in:NSRect(x:0,y:0,width:size,height:size),from:.zero,operation:.copy,fraction:1)
    NSGraphicsContext.restoreGraphicsState()
    frames.append(bitmap.representation(using:.png,properties:[:])!)
}
var ico=Data()
func word(_ n:Int) { var v=UInt16(n).littleEndian;withUnsafeBytes(of:&v){ico.append(contentsOf:$0)} }
func dword(_ n:Int) { var v=UInt32(n).littleEndian;withUnsafeBytes(of:&v){ico.append(contentsOf:$0)} }
word(0);word(1);word(sizes.count)
var offset=6+16*sizes.count
for (i,size) in sizes.enumerated() { ico.append(contentsOf:[UInt8(size==256 ? 0:size),UInt8(size==256 ? 0:size),0,0]);word(1);word(32);dword(frames[i].count);dword(offset);offset += frames[i].count }
for frame in frames { ico.append(frame) }
try ico.write(to:URL(fileURLWithPath:output))
print("Generated ICO frames: \(sizes)")
