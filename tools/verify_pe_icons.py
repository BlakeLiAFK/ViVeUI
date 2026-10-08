"""Verify every ICO frame is present, byte-for-byte, in each packaged PE resource tree."""
import pathlib, struct, sys, hashlib

def verify(executable, ico):
    data = pathlib.Path(executable).read_bytes()
    u16 = lambda p: struct.unpack_from('<H', data, p)[0]
    u32 = lambda p: struct.unpack_from('<I', data, p)[0]
    pe = u32(60)
    assert data[pe:pe+4] == b'PE\0\0'
    optional = pe + 24
    directories = optional + (112 if u16(optional) == 0x20b else 96)
    resource_rva = u32(directories + 16)
    sections = optional + u16(pe + 20)
    def offset(rva):
        for n in range(u16(pe + 6)):
            p = sections + n * 40
            size, address, rawsize, raw = struct.unpack_from('<IIII', data, p + 8)
            if address <= rva < address + max(size, rawsize):
                return raw + rva - address
        raise AssertionError('RVA outside sections')
    root = offset(resource_rva)
    leaves = {}
    def walk(relative, path=()):
        p = root + relative
        for n in range(u16(p+12) + u16(p+14)):
            name, target = struct.unpack_from('<II', data, p+16+n*8)
            key = path + (name,)
            if target & 0x80000000:
                walk(target & 0x7fffffff, key)
            else:
                rva, size = struct.unpack_from('<II', data, root+target)
                leaves[key] = data[offset(rva):offset(rva)+size]
    walk(0)
    expected = []
    for n in range(struct.unpack_from('<H', ico, 4)[0]):
        p = 6+n*16
        size, start = struct.unpack_from('<II', ico, p+8)
        expected.append((ico[p] or 256, ico[start:start+size]))
    groups = [value for key,value in leaves.items() if key[0] == 14]
    assert groups, 'No shell icon group'
    for group in groups:
        frames = []
        for n in range(struct.unpack_from('<H', group, 4)[0]):
            p = 6+n*14
            iconid = struct.unpack_from('<H', group, p+12)[0]
            candidates = [value for key,value in leaves.items() if key[:2] == (3,iconid)]
            assert candidates and all(value == candidates[0] for value in candidates)
            frames.append((group[p] or 256,candidates[0]))
        if frames == expected:
            print(f'PASS {executable}: {len(frames)} exact embedded ICO frames, sizes {[s for s,_ in frames]}')
            return
    raise AssertionError('No PE icon group matches all source frames')

if __name__ == '__main__':
    ico = pathlib.Path('src/ViVeUI.Windows/Assets/AppIcon.ico').read_bytes()
    for executable in sys.argv[1:]:
        verify(executable, ico)
