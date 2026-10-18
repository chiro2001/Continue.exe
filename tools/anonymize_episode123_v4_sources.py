#!/usr/bin/env python3
"""Make anonymous display copies while preserving the archival source files."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'art/reference/episode123-v4/anonymized-display'


def main():
    score_path = ROOT / 'data/episode123-v4-score.json'
    audit = json.loads((ROOT / 'data/episode123-v4-anonymity-audit.json').read_text())
    score = json.loads(score_path.read_text())
    by_id = {row['id']: row for row in score['images']}
    OUT.mkdir(parents=True, exist_ok=True)
    result = []
    for item in audit['anonymization_items']:
        row = by_id[item['id']]
        source = Path(row['source_path'])
        original_hash = hashlib.sha256(source.read_bytes()).hexdigest()
        assert original_hash == row['sha256']
        image = Image.open(source).convert('RGB')
        assert list(image.size) == item['source_size']
        draw = ImageDraw.Draw(image)
        for region in item['regions']:
            x, y, w, h = [region[key] for key in ['x', 'y', 'w', 'h']]
            assert 0 <= x < x + w <= image.width and 0 <= y < y + h <= image.height
            background = max(image.crop((x, y, x + w, y + h)).getcolors(w*h), key=lambda item: item[0])[1]
            foreground = (42, 49, 57) if sum(background) > 384 else (229, 235, 240)
            draw.rectangle((x, y, x + w - 1, y + h - 1), fill=background)
            size = max(8, round(h * .62))
            while True:
                font = ImageFont.truetype('/usr/share/fonts/noto/NotoSans-Regular.ttf', size)
                box = draw.textbbox((0, 0), 'xxxx', font=font)
                if box[2] - box[0] <= w - 2 and box[3] - box[1] <= h - 2:
                    break
                size -= 1
                assert size >= 5
            draw.text((x + 1 - box[0], y + (h - (box[3] - box[1])) // 2 - box[1]), 'xxxx', font=font, fill=foreground)
        destination = OUT / (item['id'] + '-anonymous.png')
        image.save(destination)
        assert hashlib.sha256(source.read_bytes()).hexdigest() == original_hash
        row['display_source_path'] = str(destination)
        row['display_path'] = 'Assets/OldWebMap/Episode123/Revision4/Images/' + destination.name
        row['display_sha256'] = hashlib.sha256(destination.read_bytes()).hexdigest()
        row['display_processing'] = 'Personal account labels replaced by xxxx in an audited display copy; archival source preserved.'
        row['anonymous_regions'] = item['regions']
        result.append(dict(id=item['id'], display_source_path=str(destination), regions=len(item['regions']), source_unchanged=True))
    score_path.write_text(json.dumps(score, ensure_ascii=False, indent=2) + '\n')
    report = dict(success=True, display_copies=len(result), anonymous_regions=sum(row['regions'] for row in result), unique_content_count_unchanged=len(score['images']), items=result)
    report_path = Path.home() / 'tmp/vrc-old-web-map/episode123-v4/reports/anonymity-sources.json'
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
