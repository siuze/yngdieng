﻿using System.Linq;
using Yngdieng.Protos;

namespace ZingzeuOrg.Yngdieng.Web
{

    public static class RichTextUtil
    {
        public static RichText FromString(string s)
        {
            return new RichText
            {
                Segments = {
                    new RichText.Types.Segment {
                        Text = s,
                        Highlighted = false
                    }
                }
            };
        }

        public static string Flatten(RichText r)
        {
            return string.Join(string.Empty, r.Segments.Select(s => s.Text));
        }
    }
}
