"""Generate a draw.io use case diagram for one actor.

Usage: python gen_use_case_diagram.py "Clinic Assistant"   (or "all")
Reads use case names from ../USE-CASE-CATALOG.md and writes use-cases-<actor>.drawio here.
Open the file in draw.io (File > Open, or File > Import).

Layout: the use cases the actor starts directly sit on an arc around the actor, ascending from top
to bottom. A use case that «extend»s another is reached from its base use case, so it is not linked
to the actor; it is placed outside the arc next to its base and joined with a straight dashed arrow.
"""
import math
import re
import sys
from pathlib import Path
from xml.sax.saxutils import escape

HERE = Path(__file__).resolve().parent
CATALOG = HERE.parent / "USE-CASE-CATALOG.md"

# use_cases: every use case of the actor, ascending.
# extends:   (extending, base) pairs checked against the web screens; see the catalog.
# placement: centre of each extending use case as an offset (dx, dy) from a use case on the arc.
# on_arc:    extending use cases the actor also opens directly (own menu entry): kept on the arc and
#            linked to the actor; their «extend» arrow runs outside the arc with two right-angle bends.
DIAGRAMS = {
    "Clinic Assistant": {
        "use_cases": [15, 16, 17, 18, 19, 20, 23, 24, 25, 26, 29, 35, 36, 37, 39, 41, 42],
        "extends": [(16, 15), (17, 15), (18, 15), (16, 18), (16, 23),
                    (17, 24), (18, 24), (19, 24), (24, 23), (35, 36)],
        "placement": {16: (15, 357, 78), 17: (15, 697, -132), 18: (15, 507, -22),
                      24: (23, 547, 59), 19: (23, 547, 189), 35: (36, 330, 0)},
    },
    "Dentist": {
        "use_cases": [15, 16, 17, 18, 19, 21, 24, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 40],
        "extends": [(16, 15), (17, 15), (18, 15), (16, 18), (17, 24), (18, 24), (19, 24),
                    (28, 27), (29, 27), (29, 28), (30, 29), (31, 27), (38, 27),
                    (35, 32), (35, 36), (37, 32)],
        "placement": {16: (15, 266, -112), 17: (15, 266, 48), 18: (15, 746, -102), 19: (24, 324, -88),
                      28: (27, 306, -71), 29: (27, 616, 19), 30: (27, 616, 159), 38: (27, 306, 79),
                      31: (27, 306, 199), 37: (32, 323, 200), 35: (32, 323, 330)},
    },
    "Guest": {
        "use_cases": [2, 3, 7, 8, 9, 10, 13],
        "extends": [(8, 13)],
        "on_arc": [8],
        "placement": {},
    },
    "Patient": {
        "use_cases": [3, 7, 8, 9, 11, 12, 13, 14, 29],
        "extends": [(8, 13), (14, 13)],
        "on_arc": [8],
        "placement": {14: (13, 380, 22)},
    },
    "Clinic Owner": {
        "use_cases": [6, 15, 21, 22, 33, 34, 40, 41, 42, 43, 45, 46],
        "extends": [],
        "placement": {},
    },
}
# Secondary actors, drawn right of the system boundary on every diagram that shows their use cases.
SECONDARY = {
    "Notification Service": [7],
    "Xray/PACS System": [28, 29],
    "Payment Gateway": [39],
}

UC_W, UC_H = 180, 64
ARC_STEP = 110         # spacing between use case centres along the arc
HALF_SPAN = math.radians(54)


def names():
    result = {}
    for line in CATALOG.read_text(encoding="utf-8").splitlines():
        m = re.match(r"\| UC-(\d+) \| ([^|]+?) \| [^|]+ \| [^|]+ \| ", line)
        if m:
            result[int(m.group(1))] = m.group(2).strip()
    return result


def build(actor):
    spec = DIAGRAMS[actor]
    extends = spec["extends"]
    extending = {src for src, _ in extends}
    on_arc = set(spec.get("on_arc", []))
    arc = [uc for uc in spec["use_cases"] if uc not in extending or uc in on_arc]
    uc_name = names()

    n = len(arc)
    radius = max(420, ARC_STEP * (n - 1) / (2 * HALF_SPAN))
    half = min(HALF_SPAN, ARC_STEP * (n - 1) / (2 * radius))
    actor_x, actor_w, actor_h = 60, 40, 80
    cx = actor_x + actor_w / 2
    cy = 220 + UC_H / 2 + radius * math.sin(half)
    centre = {}
    for i, uc in enumerate(arc):
        a = -half + i * (2 * half / (n - 1)) if n > 1 else 0.0
        centre[uc] = (cx + radius * math.cos(a), cy + radius * math.sin(a))
    for uc, (anchor, dx, dy) in spec["placement"].items():
        ax, ay = centre[anchor]
        centre[uc] = (ax + dx, ay + dy)

    # «extend» between two use cases on the arc: vertical lane right of the use cases in between.
    lane_pts = {}
    for src, dst in extends:
        if src in arc and dst in arc:
            i, j = sorted((arc.index(src), arc.index(dst)))
            lane_x = max(centre[uc][0] for uc in arc[i:j + 1]) + UC_W / 2 + 40
            lane_pts[(src, dst)] = [(lane_x, centre[src][1]), (lane_x, centre[dst][1] - UC_H * 0.15)]

    xs = [x for x, _ in centre.values()] + [pts[0][0] - UC_W / 2 + 20 for pts in lane_pts.values()]
    ys = [y for _, y in centre.values()]
    top, bottom = min(ys) - UC_H / 2 - 70, max(ys) + UC_H / 2 + 40
    left, right = min(xs) - UC_W / 2 - 60, max(xs) + UC_W / 2 + 50

    # Secondary actors: right of the boundary, level with their use cases, at least 150 px apart.
    secondary = []
    for name, links in SECONDARY.items():
        linked = [uc for uc in links if uc in centre]
        if linked:
            secondary.append([sum(centre[uc][1] for uc in linked) / len(linked), name, linked])
    secondary.sort()
    for i in range(1, len(secondary)):
        secondary[i][0] = max(secondary[i][0], secondary[i - 1][0] + 150)
    sec_x = right + 110

    actor_style = "shape=umlActor;verticalLabelPosition=bottom;verticalAlign=top;html=1;outlineConnect=0;"
    cells = [
        f'<mxCell id="boundary" value="DCMS" style="rounded=0;whiteSpace=wrap;html=1;fillColor=none;verticalAlign=top;'
        f'align=center;fontStyle=1;fontSize=20;spacingTop=8;" vertex="1" parent="1">'
        f'<mxGeometry x="{left:.0f}" y="{top:.0f}" width="{right - left:.0f}" height="{bottom - top:.0f}" as="geometry"/></mxCell>',
        f'<mxCell id="actor" value="{escape(actor)}" style="{actor_style}" vertex="1" parent="1">'
        f'<mxGeometry x="{actor_x}" y="{cy - actor_h / 2:.0f}" width="{actor_w}" height="{actor_h}" as="geometry"/></mxCell>',
        f'<mxCell id="registered" value="Registered User" style="{actor_style}" vertex="1" parent="1">'
        f'<mxGeometry x="{actor_x}" y="{top:.0f}" width="{actor_w}" height="{actor_h}" as="geometry"/></mxCell>',
        '<mxCell id="gen" style="endArrow=block;endFill=0;endSize=14;html=1;" edge="1" parent="1" '
        'source="actor" target="registered"><mxGeometry relative="1" as="geometry"/></mxCell>',
    ]
    for uc in spec["use_cases"]:
        x, y = centre[uc]
        label = escape(f"UC-{uc:02d}<br>{uc_name.get(uc, '')}")
        cells.append(f'<mxCell id="uc{uc}" value="{label}" style="ellipse;whiteSpace=wrap;html=1;fontSize=11;" '
                     f'vertex="1" parent="1"><mxGeometry x="{x - UC_W / 2:.0f}" y="{y - UC_H / 2:.0f}" '
                     f'width="{UC_W}" height="{UC_H}" as="geometry"/></mxCell>')
    for uc in arc:
        cells.append(f'<mxCell id="as{uc}" style="endArrow=none;html=1;" edge="1" parent="1" source="actor" '
                     f'target="uc{uc}"><mxGeometry relative="1" as="geometry"/></mxCell>')
    for i, (y, name, linked) in enumerate(secondary):
        cells.append(f'<mxCell id="sec{i}" value="{escape(name)}" style="{actor_style}" vertex="1" parent="1">'
                     f'<mxGeometry x="{sec_x:.0f}" y="{y - actor_h / 2:.0f}" width="{actor_w}" height="{actor_h}" as="geometry"/></mxCell>')
        for uc in linked:
            cells.append(f'<mxCell id="sec{i}uc{uc}" style="endArrow=none;html=1;" edge="1" parent="1" '
                         f'source="uc{uc}" target="sec{i}"><mxGeometry relative="1" as="geometry"/></mxCell>')
    page_right = sec_x + 160 if secondary else right + 60
    for i, (src, dst) in enumerate(extends):
        style = "endArrow=open;endSize=10;dashed=1;html=1;fontSize=11;labelBackgroundColor=#ffffff;"
        points = ""
        if (src, dst) in lane_pts:
            style += "rounded=0;exitX=1;exitY=0.5;exitDx=0;exitDy=0;entryX=1;entryY=0.35;entryDx=0;entryDy=0;"
            points = '<Array as="points">' + "".join(
                f'<mxPoint x="{x:.0f}" y="{y:.0f}"/>' for x, y in lane_pts[(src, dst)]) + "</Array>"
        cells.append(f'<mxCell id="ext{i}" value="&amp;lt;&amp;lt;extend&amp;gt;&amp;gt;" style="{style}" '
                     f'edge="1" parent="1" source="uc{src}" target="uc{dst}">'
                     f'<mxGeometry relative="1" as="geometry">{points}</mxGeometry></mxCell>')

    return (f'<mxfile host="app.diagrams.net"><diagram id="{actor.replace(" ", "-").lower()}" name="{escape(actor)}">'
            f'<mxGraphModel dx="1400" dy="900" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" '
            f'fold="1" page="1" pageScale="1" pageWidth="{page_right:.0f}" pageHeight="{bottom + 60:.0f}" math="0" shadow="0">'
            f'<root><mxCell id="0"/><mxCell id="1" parent="0"/>{"".join(cells)}</root></mxGraphModel></diagram></mxfile>\n')


if __name__ == "__main__":
    actor = sys.argv[1] if len(sys.argv) > 1 else "Clinic Assistant"
    actors = list(DIAGRAMS) if actor == "all" else [actor]
    for name in actors:
        out = HERE / f"use-cases-{name.replace(' ', '-').lower()}.drawio"
        out.write_text(build(name), encoding="utf-8")
        print(out)
