import csv
import hashlib
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter
from inspect_vocabulary import paragraphs

POS = re.compile(r'[〔［\[][^〕］\]]+[〕］\]]')
LESSON = re.compile(r'^第(\d+)课$')
KANA = re.compile(r'[\u3040-\u30ff]')
PAREN_HEAD = re.compile(r'^([^\s（(]+)[（(]([^）)]+)[）)](.*)$')
MISSING = '【原文未提供中文释义】'

def split_entry(text):
    if text.startswith(('～', '何なん', 'お～', 'ご～', 'あと～')):
        return text, '', MISSING
    match = POS.search(text)
    if match:
        head = text[:match.start()].strip()
        meaning = text[match.start():].strip()
        if not head or not text[match.end():].strip():
            return None
    else:
        match = PAREN_HEAD.match(text)
        if match and match[3].strip():
            head = text[:match.start(3)].strip()
            meaning = match[3].strip().lstrip('／').strip()
        else:
            parts = re.split(r'\s+', text, maxsplit=1)
            if len(parts) == 2:
                head, meaning = parts
            else:
                # A kana-only phrase immediately followed by a Chinese meaning.
                match = re.match(r'^([\u3040-\u30ff]+)([\u4e00-\u9fff].*)$', text)
                if not match:
                    return None
                head, meaning = match.groups()
    written_parts, reading_parts = [], []
    for part in re.split(r'([∕／])', head):
        if part in ('∕', '／'):
            written_parts.append(part)
            reading_parts.append(part)
            continue
        match = PAREN_HEAD.match(part)
        if match and not match[3].strip():
            reading, written = match[1], match[2]
            written_parts.append(written if not re.search('[～~]', written) else part)
            reading_parts.append(reading)
        else:
            written_parts.append(part)
            reading_parts.append(part if KANA.search(part) else '')
    japanese, reading = ''.join(written_parts), ''.join(reading_parts)
    return japanese, reading, meaning

def convert(source, output):
    output.mkdir(parents=True, exist_ok=True)
    source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
    rows, tags = paragraphs(source)
    entries, classifications, pending = [], [], []
    unit = None

    def flush():
        if not pending:
            return
        text = ''.join(x['text'].strip() for x in pending)
        parsed = split_entry(text)
        if parsed is None:
            raise ValueError('Unparsed entry: ' + repr(pending))
        japanese, reading, meaning = parsed
        # The source places the closing parenthesis after the POS tag on this line.
        # Preserve it verbatim in SourceText while separating the display fields.
        if pending[0]['id'] == '1970.1' and text == 'あキューせいでん（阿Q正伝［专］）阿Q正传':
            japanese, reading, meaning = '阿Q正伝', 'あキューせいでん', '［专］阿Q正传'
        record = dict(Japanese=japanese, Pinyin=reading, Chinese=meaning, Unit=unit,
                      SourceId='shinnichi:' + source_hash + ':' + pending[0]['id'],
                      SourceText='\n'.join(x['text'] for x in pending),
                      Segments=[x['id'] for x in pending])
        entries.append(record)
        for part in pending:
            part['record'] = len(entries)
            part['kind'] = 'entry'
        pending.clear()

    for number, paragraph in rows:
        for line, original in enumerate(paragraph.split('\n'), 1):
            part = dict(id=f'{number}.{line}', text=original)
            classifications.append(part)
            text = original.strip()
            lesson = LESSON.match(text)
            if lesson or not text or re.fullmatch('-+', text):
                flush()
                part['kind'] = 'heading' if lesson else 'blank' if not text else 'separator'
                if lesson:
                    unit = '第%02d课' % int(lesson[1])
                continue
            if unit is None:
                raise ValueError('Entry outside a lesson')
            if pending:
                prior = ''.join(x['text'].strip() for x in pending)
                # Wrapped entries are joined only while the previous entry is incomplete.
                if split_entry(prior) is not None:
                    flush()
            pending.append(part)
    flush()
    if set(e['Unit'] for e in entries) != {'第%02d课' % n for n in range(1, 49)}:
        raise ValueError('Expected all 48 lessons')
    if len({e['SourceId'] for e in entries}) != len(entries):
        raise ValueError('Duplicate source identifiers')
    for index, e in enumerate(entries, 1):
        expected = '\n'.join(p['text'] for p in classifications if p.get('record') == index)
        assert e['SourceText'] == expected
    root = ET.Element('WordDatabase')
    words = ET.SubElement(root, 'Words')
    for entry in entries:
        node = ET.SubElement(words, 'WordItem')
        for field in ('Japanese', 'Pinyin', 'Chinese', 'Unit', 'SourceId', 'SourceText'):
            ET.SubElement(node, field).text = entry[field]
    ET.indent(root, space='  ')
    xml_path = output / '标日1-48课-完整词库.xml'
    ET.ElementTree(root).write(xml_path, encoding='utf-8', xml_declaration=True)
    loaded = ET.parse(xml_path).findall('./Words/WordItem')
    for before, after in zip(entries, loaded):
        for field in ('Japanese', 'Pinyin', 'Chinese', 'Unit', 'SourceId', 'SourceText'):
            assert before[field] == (after.findtext(field) or '')
    assert len(loaded) == len(entries)
    audit = dict(SourceFile=source.name, SourceSha256=source_hash,
                 Paragraphs=[dict(number=n, text=t) for n,t in rows],
                 Segments=classifications, Entries=entries,
                 Counts=dict(Paragraphs=len(rows), Entries=len(entries),
                             Units=dict(Counter(e['Unit'] for e in entries)),
                             MissingMeanings=sum(e['Chinese'] == MISSING for e in entries)))
    (output / '转换核对.json').write_text(json.dumps(audit, ensure_ascii=False, indent=2), encoding='utf-8')
    with (output / '逐条核对.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.writer(stream)
        writer.writerow(['课次','单词','假名','意思','原文','来源段落'])
        for e in entries:
            writer.writerow([e['Unit'], e['Japanese'], e['Pinyin'], e['Chinese'], e['SourceText'], ','.join(e['Segments'])])
    report = [
        '# 标日词库转换核对', '',
        '原始文件：' + source.name,
        '原始文件 SHA256：`' + source_hash + '`', '',
        '## 核对结果', '',
        f'- 原文 {len(rows)} 个段落全部保留在转换核对.json 中，包括课次、分隔线和空白段落。',
        f'- {sum(len(e["Segments"]) for e in entries)} 段词条内容对应到导入记录，保留来源编号。',
        f'- 跨行合并 {sum(len(e["Segments"]) > 1 for e in entries)} 条，最终共 {len(entries)} 条记录，覆盖第 01 至第 48 课。',
        '- 同形词按原始记录分别保留，没有按日语单词去重或覆盖。',
        '- 每条 XML 记录的 SourceText 保存逐字原文；SourceId 保存稳定来源编号。',
        f'- {audit["Counts"]["MissingMeanings"]} 条原文未给中文释义的量词、接头词等保留原样，并标注“原文未提供中文释义”。',
        '- 原文疑似错字不自动纠正。第 1970 段“阿Q正伝”的括号与词性位置异常，仅在显示字段中重新分栏，SourceText 原样保留。',
        '- 转换文件写入 XML 后重新读取，全部记录逐字段比对通过。软件导入与存储测试入口是 tests/ImportTests.cs。', '',
        '## 文件用途', '',
        '- 标日1-48课-完整词库.xml：唯一推荐的导入文件。',
        '- 逐条核对.csv：仅用于表格查看和核对，不能用旧版三列导入器导入。',
        '- 转换核对.json：完整原文、逐段分类、记录与来源对应关系。', '',
        '## 各课记录数', '', '| 课次 | 条数 |', '| --- | --- |'
    ]
    report += ['| ' + unit + ' | ' + str(count) + ' |' for unit, count in audit['Counts']['Units'].items()]
    report += ['', '## 原文缺少中文释义的项目', '']
    report += ['- ' + e['Unit'] + '：' + e['Japanese'] for e in entries if e['Chinese'] == MISSING]
    (output / '核对说明.md').write_text('\n'.join(report) + '\n', encoding='utf-8')
    print(json.dumps(audit['Counts'], ensure_ascii=False, indent=2))
    print('MISSING:', [(e['Segments'], e['Japanese']) for e in entries if e['Chinese'] == MISSING])

if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    convert(Path(sys.argv[1]), Path(sys.argv[2]))
