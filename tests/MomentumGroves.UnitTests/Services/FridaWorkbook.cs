using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace MomentumGroves.Tests.Services;

// Small genuine XLSX files keep import tests independent of downloads and Excel installations.
internal static class FridaWorkbook
{
    /// <summary>
    /// Builds a synthetic Frida workbook in memory.
    /// </summary>
    /// <param name="foods">The number of synthetic food rows to generate.</param>
    /// <param name="includeDataSheet">Whether the workbook includes the nutrition worksheet.</param>
    /// <returns>The generated workbook as a byte array.</returns>
    public static byte[] Create(int foods, bool includeDataSheet = true)
    {
        XNamespace sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var rows = new XElement(sheet + "sheetData");
        for (var index = 1; index <= foods + 5; index++)
        {
            var row = new XElement(sheet + "row", new XAttribute("r", index));
            /// <summary>
            /// Writes a text cell into the current worksheet row.
            /// </summary>
            /// <param name="column">The worksheet column name for the test cell.</param>
            /// <param name="value">The test value supplied for the field.</param>
            void Text(string column, string value)
            {
                row.Add(new XElement(sheet + "c",
                new XAttribute("r", column + index), new XAttribute("t", "inlineStr"),
                new XElement(sheet + "is", new XElement(sheet + "t", value))));
            }
            /// <summary>
            /// Writes a numeric cell into the current worksheet row.
            /// </summary>
            /// <param name="column">The worksheet column name for the test cell.</param>
            /// <param name="value">The test value supplied for the field.</param>
            void Number(string column, double value)
            {
                row.Add(new XElement(sheet + "c",
                new XAttribute("r", column + index), new XElement(sheet + "v", value)));
            }
            if (index <= 4)
            {
                Text("A", "Header");
            }
            else if (index == 5)
            {
                Text("A", "   ");
            } // Blank names are skipped.
            else
            {
                Text("A", " Food " + (index - 5) + " ");
                // Alternate complete and missing/invalid nutrient data to exercise nullable storage.
                if (index % 2 == 0)
                {
                    Text("B", "English food"); Number("F", 100.5); Number("H", 2); Number("L", 20); Number("O", 3);
                }
                else { Text("F", "invalid"); Text("H", " "); }
            }
            rows.Add(row);
        }

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            /// <summary>
            /// Writes an XML entry to the workbook archive.
            /// </summary>
            /// <param name="path">The entry path inside the workbook archive.</param>
            /// <param name="content">The XML content written to the archive entry.</param>
            void Add(string path, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open(), Encoding.UTF8);
                writer.Write(content);
            }

            Add("[Content_Types].xml", """
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                  <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            Add("_rels/.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            Add("xl/workbook.xml", new XDocument(new XElement(sheet + "workbook",
                new XAttribute(XNamespace.Xmlns + "r", relationships),
                new XElement(sheet + "sheets",
                    new XElement(sheet + "sheet", new XAttribute("name", "Notes"), new XAttribute("sheetId", 1), new XAttribute(relationships + "id", "rId1")),
                    new XElement(sheet + "sheet", new XAttribute("name", includeDataSheet ? "Data_Table" : "Other"), new XAttribute("sheetId", 2), new XAttribute(relationships + "id", "rId2"))))).ToString());
            Add("xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
                </Relationships>
                """);
            Add("xl/worksheets/sheet1.xml", new XElement(sheet + "worksheet", new XElement(sheet + "sheetData")).ToString());
            Add("xl/worksheets/sheet2.xml", new XElement(sheet + "worksheet", rows).ToString());
        }
        return output.ToArray();
    }
}
