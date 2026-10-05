from collections import Counter
from pathlib import Path
import re
import sys
import zipfile
import xml.etree.ElementTree as ET

NS = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
W = '{' + NS['w'] + '}'

def paragraphs(path):
    with zipfile.ZipFile(path) as archive:
        root = ET.fromstring(archive.read('word/document.xml'))
        result = []
        for number, paragraph in enumerate(root.findall('.//w:body//w:p', NS), 1):
            fragments = []
            for node in paragraph.iter():
                if node.tag == W + 't':
                    fragments.append(node.text or '')
                elif node.tag in (W + 'tab', W + 'br', W + 'cr'):
                    fragments.append('\t' if node.tag == W + 'tab' else '\n')
            result.append((number, ''.join(fragments)))
        return result, Counter(n.tag.split('}')[-1] for n in root.iter())

if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    rows, tags = paragraphs(sys.argv[1])
    print('TAGS', tags)
    print('WITHOUT POS:')
    for number, text in rows:
        if text.strip() and not re.search(r'[〔［\[][^〕］\]]+[〕］\]]', text):
            print(number, repr(text))
