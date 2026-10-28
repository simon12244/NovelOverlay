# -*- coding: utf-8 -*-
"""生成 txt / epub / mobi 三份测试样书，用于验证 NovelOverlay 的解析路径"""
import io, os, struct, zipfile

BASE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(BASE), 'test')
if not os.path.isdir(OUT):
    os.makedirs(OUT)

TITLE = '深蓝夜航'
CHAPTERS = [
    ('第一章 灯下的字母', [
        '夜里十一点，整座城市像被人按住了呼吸。楼下的便利店还亮着，白得有点晃眼，收银台后面的人正低头刷手机。',
        '林澈把台灯拧到最暗的一档，光落在桌面上，刚好照住那本旧笔记本。封皮是深蓝色的，边角被磨得发白，像一块被反复擦拭的石头。',
        '他翻开第一页。上面的字迹陌生又熟悉：笔画很轻，收尾时总是习惯性地往上挑一点。那是母亲的字。',
        '“如果你看到这些字，说明你终于愿意打开它了。”',
        '窗外的风把玻璃敲了两下。林澈没有抬头，只是把手指按在那行字上，好像在确认它是热的。',
    ]),
    ('第二章 旧地图', [
        '笔记本的最后一页夹着一张折了四次的地图。纸很脆，展开的时候能听见细小的响声，像有人在耳边低声说话。',
        '地图上没有城市，没有公路，只有一个用铅笔描了很多遍的圆圈，圈里写着两个字：归港。',
        '林澈在地图上找了很久，才在一段几乎被磨平的海岸线旁边，认出一个早已废弃的地名。那地方他小时候去过一次，坐了很久的长途车，下车时鞋里全是沙。',
        '他忽然想起一件小事：那天傍晚，母亲站在海边，风把她的头发吹得很乱，她却一直笑着，说“以后你会明白的”。',
        '那时候他八岁，只顾着捡贝壳。',
    ]),
    ('第三章 起风了', [
        '第二天清晨，他把窗帘拉开，发现天是那种很少见的青灰色，云压得很低，像要落下来。',
        '行李箱里只放了三样东西：笔记本、地图、一件旧外套。他站在门口看了很久，最后还是没有把钥匙带走。',
        '楼道里的声控灯坏了一盏，走到三楼时突然黑下来。他站在原地等了两秒，然后继续往上，一步一步，脚步声在窄窄的空间里回响。',
        '推开门，风从走廊尽头扑过来，带着一点海的味道。',
        '他知道那不是错觉。',
    ]),
]


def plain_text():
    lines = [TITLE, '']
    for t, paras in CHAPTERS:
        lines.append(t)
        lines.append('')
        for p in paras:
            lines.append('    ' + p)
            lines.append('')
    return '\n'.join(lines)


def write_txt():
    p = os.path.join(OUT, '示例小说.txt')
    io.open(p, 'w', encoding='utf-8').write(plain_text())
    print('txt  ->', p)


XHTML_HEAD = ('<?xml version="1.0" encoding="utf-8"?>\n'
              '<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.1//EN" '
              '"http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd">\n'
              '<html xmlns="http://www.w3.org/1999/xhtml"><head><title>{t}</title>'
              '<meta http-equiv="Content-Type" content="text/html; charset=utf-8"/></head><body>'
              '<h2>{t}</h2>\n{body}\n</body></html>')


def write_epub():
    p = os.path.join(OUT, '示例.epub')
    z = zipfile.ZipFile(p, 'w', zipfile.ZIP_DEFLATED)
    z.writestr(zipfile.ZipInfo('mimetype'), 'application/epub+zip', zipfile.ZIP_STORED)
    z.writestr('META-INF/container.xml',
               '<?xml version="1.0"?>\n<container version="1.0" '
               'xmlns="urn:oasis:names:tc:opendocument:xmlns:container">'
               '<rootfiles><rootfile full-path="OEBPS/content.opf" '
               'media-type="application/oebps-package+xml"/></rootfiles></container>')
    items, spine = [], []
    for i, (t, paras) in enumerate(CHAPTERS, 1):
        body = '\n'.join('<p>%s</p>' % x for x in paras)
        z.writestr('OEBPS/ch%d.xhtml' % i, XHTML_HEAD.format(t=t, body=body))
        items.append('<item id="c%d" href="ch%d.xhtml" media-type="application/xhtml+xml"/>' % (i, i))
        spine.append('<itemref idref="c%d"/>' % i)
    opf = ('<?xml version="1.0" encoding="utf-8"?>\n'
           '<package xmlns="http://www.idpf.org/2007/opf" version="2.0" unique-identifier="uid">'
           '<metadata xmlns:dc="http://purl.org/dc/elements/1.1/">'
           '<dc:title>%s</dc:title><dc:language>zh-CN</dc:language>'
           '<dc:identifier id="uid">noveloverlay-test</dc:identifier></metadata>'
           '<manifest>%s<item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/></manifest>'
           '<spine toc="ncx">%s</spine></package>') % (TITLE, ''.join(items), ''.join(spine))
    z.writestr('OEBPS/content.opf', opf)
    z.writestr('OEBPS/toc.ncx',
               '<?xml version="1.0"?><ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1">'
               '<head/><docTitle><text>t</text></docTitle><navMap/></ncx>')
    z.close()
    print('epub ->', p)


def write_mobi():
    text = plain_text().encode('utf-8')
    name = TITLE.encode('utf-8')

    mobi_head_len = 232
    rec0 = bytearray()
    rec0 += struct.pack('>HHIHHH', 1, 0, len(text), 1, 4096, 0)
    rec0 += b'\x00\x00'          # PalmDOC 头的 2 字节 unknown，共 16 字节
    rec0 += b'MOBI'
    rec0 += struct.pack('>I', mobi_head_len)
    rec0 += struct.pack('>I', 2)
    rec0 += struct.pack('>I', 65001)
    rec0 += struct.pack('>I', 1234567)
    rec0 += struct.pack('>I', 6)
    while len(rec0) < 84:
        rec0 += b'\x00'
    name_off = 16 + mobi_head_len
    rec0 += struct.pack('>I', name_off)
    rec0 += struct.pack('>I', len(name))
    while len(rec0) < name_off:
        rec0 += b'\x00'
    rec0 += name
    while len(rec0) % 2:
        rec0 += b'\x00'

    rec1 = text
    if len(rec1) % 2:
        rec1 += b'\x00'

    num = 2
    hdr = bytearray(78)
    nm = TITLE.encode('ascii', 'ignore') or b'NovelOverlay'
    hdr[0:len(nm[:31])] = nm[:31]
    hdr[60:64] = b'BOOK'
    hdr[64:68] = b'MOBI'
    struct.pack_into('>H', hdr, 76, num)

    data_off = 78 + 8 * num + 2
    off0 = data_off
    off1 = off0 + len(rec0)

    reclist = bytearray()
    reclist += struct.pack('>IB', off0, 0) + b'\x00\x00\x01'
    reclist += struct.pack('>IB', off1, 0) + b'\x00\x00\x02'
    reclist += b'\x00\x00'
    assert len(reclist) == 8 * num + 2, len(reclist)

    p = os.path.join(OUT, '示例.mobi')
    with open(p, 'wb') as f:
        f.write(bytes(hdr) + bytes(reclist) + bytes(rec0) + rec1)
    print('mobi ->', p)


write_txt()
write_epub()
write_mobi()
