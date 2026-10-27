#!/usr/bin/env python3
"""Combine the CLI's two SVG plots without changing their scale or crown symbols.
Run both generators first, then pass original.svg proton.svg comparison.svg.
Optional PNG rendering: convert comparison.svg comparison.png
"""
import argparse
from pathlib import Path
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('original', type=Path)
parser.add_argument('proton', type=Path)
parser.add_argument('output', type=Path)
args = parser.parse_args()
ns = 'http://www.w3.org/2000/svg'
ET.register_namespace('', ns)
root = ET.Element(f'{{{ns}}}svg', width='1800', height='960', viewBox='0 0 1800 960')
for i, path in enumerate((args.original, args.proton)):
    panel = ET.parse(path).getroot()
    group = ET.SubElement(root, f'{{{ns}}}g', transform=f'translate({i * 900},0)')
    for element in panel.iter():
        for key, value in list(element.attrib.items()):
            if key == 'id': element.set(key, f'panel{i}-{value}')
            elif 'url(#' in value: element.set(key, value.replace('url(#', f'url(#panel{i}-'))
    group.extend(list(panel))
args.output.parent.mkdir(parents=True, exist_ok=True)
ET.ElementTree(root).write(args.output, encoding='unicode', xml_declaration=True)
print(args.output)
