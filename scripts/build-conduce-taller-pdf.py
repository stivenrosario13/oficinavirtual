from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph, Table, TableStyle
from reportlab.pdfgen import canvas


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "public" / "documents" / "conduce-taller-grupo-samana.pdf"
LOGO = ROOT / "scratch" / "conduce-source-v2" / "xl" / "media" / "image1.png"

NAVY = colors.HexColor("#061F37")
BLUE = colors.HexColor("#0A74A6")
CYAN = colors.HexColor("#19B7D8")
PALE = colors.HexColor("#EAF7FB")
INK = colors.HexColor("#17324A")
MUTED = colors.HexColor("#617587")
LINE = colors.HexColor("#C8D8E3")
RED = colors.HexColor("#B4233C")


def register_fonts() -> tuple[str, str]:
    regular_path = Path(r"C:\Windows\Fonts\segoeui.ttf")
    bold_path = Path(r"C:\Windows\Fonts\segoeuib.ttf")
    if regular_path.exists() and bold_path.exists():
        pdfmetrics.registerFont(TTFont("SegoeUI", regular_path))
        pdfmetrics.registerFont(TTFont("SegoeUI-Bold", bold_path))
        return "SegoeUI", "SegoeUI-Bold"
    return "Helvetica", "Helvetica-Bold"


FONT, FONT_BOLD = register_fonts()


def label_value(pdf: canvas.Canvas, label: str, value: str, x: float, y: float, width: float) -> None:
    pdf.setFont(FONT_BOLD, 8.4)
    pdf.setFillColor(MUTED)
    pdf.drawString(x, y, label.upper())
    pdf.setFillColor(INK)
    pdf.setFont(FONT_BOLD, 11)
    pdf.drawString(x, y - 5.5 * mm, value)
    pdf.setStrokeColor(LINE)
    pdf.setLineWidth(0.7)
    pdf.line(x, y - 7.4 * mm, x + width, y - 7.4 * mm)


def signature(pdf: canvas.Canvas, x: float, y: float, width: float, role: str, name: str = "") -> None:
    pdf.setStrokeColor(colors.HexColor("#7890A2"))
    pdf.setLineWidth(0.8)
    pdf.line(x, y, x + width, y)
    pdf.setFillColor(INK)
    pdf.setFont(FONT_BOLD if name else FONT, 9)
    if name:
        pdf.drawCentredString(x + width / 2, y + 4 * mm, name)
    pdf.setFillColor(MUTED)
    pdf.setFont(FONT, 8.3)
    pdf.drawCentredString(x + width / 2, y - 4.3 * mm, role)


def build_pdf() -> None:
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    pdf = canvas.Canvas(str(OUTPUT), pagesize=A4, pageCompression=1)
    width, height = A4
    margin = 15 * mm

    pdf.setFillColor(colors.white)
    pdf.rect(0, 0, width, height, fill=1, stroke=0)
    pdf.setFillColor(NAVY)
    pdf.rect(0, height - 42 * mm, width, 42 * mm, fill=1, stroke=0)
    pdf.setFillColor(CYAN)
    pdf.rect(0, height - 42 * mm, 4 * mm, 42 * mm, fill=1, stroke=0)

    if LOGO.exists():
        pdf.drawImage(str(LOGO), margin, height - 35.5 * mm, width=61 * mm, height=27 * mm, preserveAspectRatio=True, mask="auto")

    pdf.setFillColor(colors.white)
    pdf.setFont(FONT_BOLD, 18)
    pdf.drawRightString(width - margin, height - 17 * mm, "CONDUCE DE TALLER")
    pdf.setFillColor(colors.HexColor("#BDEFFF"))
    pdf.setFont(FONT_BOLD, 8.5)
    pdf.drawRightString(width - margin, height - 24 * mm, "FORMULARIO DE REPARACIONES")
    pdf.setFillColor(colors.HexColor("#DCEAF2"))
    pdf.setFont(FONT, 7.8)
    pdf.drawRightString(width - margin, height - 31 * mm, "Documento de entrega, recepción y custodia")

    y = height - 55 * mm
    label_value(pdf, "Entregado por", "Richard Belén", margin, y, 76 * mm)
    label_value(pdf, "Recibido por", "Almacén", margin + 82 * mm, y, 47 * mm)
    label_value(pdf, "Agencia / grupo", "Grupo Samaná", margin, y - 19 * mm, 76 * mm)
    label_value(pdf, "Fecha", "09 oct 2025", margin + 82 * mm, y - 19 * mm, 47 * mm)
    label_value(pdf, "Vencimiento", "09 nov 2025", margin + 135 * mm, y - 19 * mm, 45 * mm)

    table_top = y - 40 * mm
    data = [
        ["CANT.", "PRODUCTO", "DESCRIPCIÓN / CONDICIÓN"],
        ["1", "Escáner Witek", ""],
        ["1", "Impresora 2Connect", ""],
        ["2", "Monitores", ""],
        ["2", "CPU", ""],
        ["2", "CPU Raza", ""],
        ["", "", ""],
        ["", "", ""],
        ["", "", ""],
    ]
    table = Table(data, colWidths=[18 * mm, 59 * mm, 103 * mm], rowHeights=[10 * mm] + [9.2 * mm] * 8)
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), BLUE),
        ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
        ("FONTNAME", (0, 0), (-1, 0), FONT_BOLD),
        ("FONTSIZE", (0, 0), (-1, 0), 8),
        ("ALIGN", (0, 0), (0, -1), "CENTER"),
        ("ALIGN", (1, 0), (-1, 0), "LEFT"),
        ("FONTNAME", (0, 1), (-1, -1), FONT),
        ("FONTSIZE", (0, 1), (-1, -1), 9.2),
        ("TEXTCOLOR", (0, 1), (-1, -1), INK),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#F7FAFC")]),
        ("GRID", (0, 0), (-1, -1), 0.55, LINE),
        ("BOX", (0, 0), (-1, -1), 1.0, colors.HexColor("#8CA4B5")),
        ("LEFTPADDING", (1, 0), (-1, -1), 5),
    ]))
    table.wrapOn(pdf, 180 * mm, 90 * mm)
    table.drawOn(pdf, margin, table_top - 83.6 * mm)

    notice_y = table_top - 93 * mm
    pdf.setFillColor(colors.HexColor("#FFF3F5"))
    pdf.roundRect(margin, notice_y - 18 * mm, 180 * mm, 18 * mm, 3 * mm, fill=1, stroke=0)
    pdf.setFillColor(RED)
    pdf.setFont(FONT_BOLD, 8.8)
    pdf.drawString(margin + 6 * mm, notice_y - 6.5 * mm, "AVISO DE CUSTODIA")
    warning = Paragraph(
        "De no retirar su equipo dentro de los próximos 30 días, será considerado en abandono y pasará a formar parte del inventario del Almacén.",
        ParagraphStyle("warning", fontName=FONT, fontSize=8.6, leading=11, textColor=INK, alignment=TA_LEFT),
    )
    warning.wrapOn(pdf, 157 * mm, 10 * mm)
    warning.drawOn(pdf, margin + 6 * mm, notice_y - 15.5 * mm)

    signatures_y = notice_y - 35 * mm
    signature(pdf, margin, signatures_y, 77 * mm, "Entregado")
    signature(pdf, margin + 103 * mm, signatures_y, 77 * mm, "Recibido", "Ángel Puente")

    reserve_y = signatures_y - 24 * mm
    pdf.setFillColor(PALE)
    pdf.roundRect(margin, reserve_y - 18 * mm, 180 * mm, 22 * mm, 3 * mm, fill=1, stroke=0)
    pdf.setFillColor(BLUE)
    pdf.setFont(FONT_BOLD, 9.5)
    pdf.drawCentredString(width / 2, reserve_y - 4 * mm, "RESERVADO PARA EL MOMENTO DE LA ENTREGA")
    signature(pdf, margin + 7 * mm, reserve_y - 14 * mm, 68 * mm, "Recibido")
    signature(pdf, margin + 105 * mm, reserve_y - 14 * mm, 68 * mm, "Entregado")

    pdf.setFillColor(NAVY)
    pdf.rect(0, 0, width, 12 * mm, fill=1, stroke=0)
    pdf.setFillColor(colors.white)
    pdf.setFont(FONT, 7.2)
    pdf.drawString(margin, 4.5 * mm, "GRUPO TEJEDA · Taller de reparaciones")
    pdf.setFillColor(colors.HexColor("#BDEFFF"))
    pdf.drawRightString(width - margin, 4.5 * mm, "Conduce de Taller · Página 1 de 1")

    pdf.setTitle("Conduce de Taller - Grupo Samaná")
    pdf.setAuthor("Grupo Tejeda")
    pdf.setSubject("Formulario de reparaciones convertido desde el conduce de taller")
    pdf.showPage()
    pdf.save()


if __name__ == "__main__":
    build_pdf()
    print(OUTPUT)
