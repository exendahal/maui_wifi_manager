"""Rebuilds src/MAUIWifiManager/Resources/oui.tsv.gz from the IEEE MA-L registry.

Usage:
    python tools/update_oui.py              # downloads the registry
    python tools/update_oui.py oui.csv      # uses a local copy

The output is one "PREFIX<TAB>Vendor" line per assignment, sorted, gzip-compressed without a
timestamp so the same registry always produces the same file. Vendor names are shortened for
display: legal suffixes, leading Chinese city names and all-caps styling are removed.
"""

import csv
import gzip
import io
import re
import sys
import urllib.request
from pathlib import Path

# Plain HTTP: the HTTPS endpoint rejects scripted downloads.
SOURCE = "http://standards-oui.ieee.org/oui/oui.csv"
OUTPUT = Path(__file__).resolve().parent.parent / "src" / "MAUIWifiManager" / "Resources" / "oui.tsv.gz"

LEGAL_SUFFIX = re.compile(
    r"[\s,.]+(co\.?,?\s*ltd\.?|co\.?|ltd\.?|limited|inc\.?|incorporated|corp\.?|corporation|corporate|"
    r"gmbh|llc|l\.l\.c\.?|s\.?a\.?|s\.?p\.?a\.?|a/s|ag|b\.?v\.?|pvt|pte|plc|oy|ab|kg|sas|srl|s\.?r\.?l\.?)$",
    re.IGNORECASE,
)

# A city name in front of a Chinese company name, e.g. "Shenzhen Jingxun Software".
LEADING_CITY = re.compile(
    r"^(shenzhen|beijing|shanghai|guangzhou|guangdong|hangzhou|dongguan|zhejiang|jiangsu|suzhou|xiamen|"
    r"fujian|chengdu|sichuan|wuhan|nanjing|tianjin|qingdao|ningbo|zhuhai|foshan|hefei|chongqing|taicang|"
    r"kunshan|huizhou|zhongshan|changsha|wuxi|jiaxing|shandong|henan|hunan|anhui)\s+",
    re.IGNORECASE,
)

# Blocks IEEE sub-assigns in its smaller registries; the name says nothing about the device.
SKIPPED = {"private", "ieee registration authority"}

# Brands whose capitals matter, after all-caps names are put in title case.
BRANDS = {"Tp-Link": "TP-Link", "Zte": "ZTE", "Lg": "LG", "Hp": "HP", "Ibm": "IBM", "Nec": "NEC", "Avm": "AVM",
          "D-Link": "D-Link", "Asustek Computer": "ASUS", "Htc": "HTC", "Tcl": "TCL", "Lcfc(Hefei) Electronics Technology": "Lenovo"}


def clean(name: str) -> str:
    name = " ".join(name.replace("\xa0", " ").split())

    previous = None
    while previous != name:
        previous = name
        name = LEGAL_SUFFIX.sub("", name).strip(" ,.&")

    name = LEADING_CITY.sub("", name)

    if name.upper() == name or name.lower() == name:
        name = " ".join(word if len(word) <= 3 and word.isupper() else word.title() for word in name.split())

    for wrong, right in BRANDS.items():
        if name.lower() == wrong.lower() or name.lower().startswith(wrong.lower() + " "):
            name = right + name[len(wrong):]

    return name


def main() -> None:
    if len(sys.argv) > 1:
        text = Path(sys.argv[1]).read_text(encoding="utf-8")
    else:
        request = urllib.request.Request(SOURCE, headers={"User-Agent": "Mozilla/5.0"})
        text = urllib.request.urlopen(request, timeout=60).read().decode("utf-8")

    rows = csv.reader(io.StringIO(text))
    next(rows)

    vendors = {}
    for row in rows:
        prefix, name = row[1].strip().upper(), row[2].strip()
        if len(prefix) == 6 and name and name.lower() not in SKIPPED:
            vendors[prefix] = clean(name)

    body = "".join(f"{prefix}\t{vendors[prefix]}\n" for prefix in sorted(vendors)).encode("utf-8")

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with open(OUTPUT, "wb") as raw, gzip.GzipFile(fileobj=raw, mode="wb", mtime=0, filename="") as packed:
        packed.write(body)

    print(f"{len(vendors)} prefixes, {len(body)} bytes, {OUTPUT.stat().st_size} bytes compressed -> {OUTPUT}")


if __name__ == "__main__":
    main()
