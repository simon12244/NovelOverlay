import io, sys

def strip(code):
    out = []
    i = 0
    n = len(code)
    BS = chr(92)
    DQ = chr(34)
    SQ = chr(39)
    while i < n:
        c = code[i]
        if c == '/' and i + 1 < n and code[i+1] == '/':
            while i < n and code[i] != '\n':
                i += 1
        elif c == '/' and i + 1 < n and code[i+1] == '*':
            i += 2
            while i + 1 < n and not (code[i] == '*' and code[i+1] == '/'):
                i += 1
            i += 2
        elif c == DQ:
            if i > 0 and code[i-1] == '@':
                i += 1
                while i < n:
                    if code[i] == DQ:
                        if i + 1 < n and code[i+1] == DQ:
                            i += 2
                            continue
                        i += 1
                        break
                    i += 1
            else:
                i += 1
                while i < n:
                    if code[i] == BS:
                        i += 2
                        continue
                    if code[i] == DQ:
                        i += 1
                        break
                    if code[i] == '\n':
                        break
                    i += 1
        elif c == SQ:
            i += 1
            while i < n:
                if code[i] == BS:
                    i += 2
                    continue
                if code[i] == SQ:
                    i += 1
                    break
                i += 1
        else:
            out.append(c)
            i += 1
    return ''.join(out)

for f in ['Settings.cs', 'Formats.cs', 'Overlay.cs', 'ReaderView.cs', 'MainForm.cs', 'Program.cs']:
    s = io.open(f, encoding='utf-8').read()
    t = strip(s)
    print(f, 'braces', t.count('{') - t.count('}'), 'parens', t.count('(') - t.count(')'), 'brackets', t.count('[') - t.count(']'))
