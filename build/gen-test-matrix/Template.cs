using System.Text;

namespace GenTestMatrix;

internal static class Template
{
    public static string Fill(string template, IDictionary<string, string> fills)
    {
        var sb = new StringBuilder(template.Length);

        var remaining = template.AsSpan();

        while (remaining.Length > 0)
        {
            var starti = remaining.IndexOf('{');
            if (starti < 0)
            {
                // no template starts left, break out
                // result will append all remaining
                break;
            }

            // there's a potential start
            if (starti + 2 >= remaining.Length)
            {
                // the template fill can't possibly be valid, we're at the end of the string
                break;
            }

            if (remaining[starti + 1] == '{')
            {
                // this is an escape sequence, append everything up-to and including starti
                sb.Append(remaining.Slice(0, starti + 1));
                remaining = remaining.Slice(starti + 2);
                continue;
            }

            // we actually have a valid start, scan for a close
            var len = remaining.Slice(starti + 1).IndexOf('}');
            if (len < 0)
            {
                // no end, break out
                break;
            }

            // append everything before
            sb.Append(remaining.Slice(0, starti));
            // now slice out the name, look it up, and fill
            var name = remaining.Slice(starti + 1, len);
            remaining = remaining.Slice(starti + 1 + len + 1);
            sb.Append(fills[name.ToString()]);
        }

        // always append any remaining
        sb.Append(remaining);

        // and return
        return sb.ToString();
    }
}
