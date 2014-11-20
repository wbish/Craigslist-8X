using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WB.SDK.Logging;

namespace WB.SDK.Parsing
{
    public static class CsvParser
    {
        public static string WriteLine(params string[] fields)
        {
            StringBuilder sb = new StringBuilder();
            string formatEscaped = @"""{0}"",";
            string formatNormal = @"{0},";

            foreach (var field in fields)
            {
                if (string.IsNullOrEmpty(field))
                {
                    sb.Append(",");
                }
                else if (field.Contains(",") || field.Contains("\""))
                {
                    sb.Append(string.Format(formatEscaped, field.Replace("\"", "\"\"")));
                }
                else
                {
                    sb.Append(string.Format(formatNormal, field));
                }
            }

            string result = sb.ToString();

            return result.Substring(0, result.Length - 1); // remove very last comma
        }

        public static List<string> ReadLine(string line)
        {
            List<string> values = new List<string>();
            int start = 0, i = 0;
            bool openQuote = false;
            string field = string.Empty;

            while (i <= line.Length)
            {
                if ((i == line.Length || line[i] == ',') && !openQuote)
                {
                    if (i == start)
                    {
                        values.Add(string.Empty);
                    }
                    else
                    {
                        field = line.Substring(start, i - start);

                        if (field.StartsWith("\""))
                        {
                            field = field.Substring(1, field.Length - 2);
                            field = field.Replace("\"\"", "\"");
                        }

                        values.Add(field);
                    }

                    start = i + 1;
                }
                else if (line[i] == '"')
                {
                    openQuote = !openQuote;
                }

                ++i;
            }

            return values;
        }
    }
}
