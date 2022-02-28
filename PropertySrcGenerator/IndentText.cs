using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace PropertySrcGenerator
{
    internal class IndentText
    {
        StringBuilder sb = new StringBuilder();
        internal int _indent = 0;

        public IDisposable Indent(bool indent = true)
        {
            return new IndentMark(this, indent);
        }

        public override string ToString()
        {
            return sb.ToString();
        }

        public void Append(string text, bool indent = false)
        {
            if (indent)
            {
                IndentTab();
            }

            sb.Append(text);
        }

        public void AppendLine(string text, bool indent = true)
        {
            if (indent)
            {
                IndentTab();
            }

            sb.AppendLine(text);
        }

        public void IndentTab()
        {
            for (int i = 0; i < _indent; i++)
            {
                sb.Append("\t");
            }
        }

        public class IndentMark : IDisposable
        {
            IndentText _outer;
            public IndentMark(IndentText outer, bool indent)
            {
                _outer = outer;

                if (indent)
                {
                    _outer._indent++;
                }
            }

            public void Dispose()
            {
                _outer._indent--;
            }
        }
    }
}
