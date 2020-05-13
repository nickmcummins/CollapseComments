﻿// Copyright (c) Matt Lacey Ltd. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace CollapseComments
{
    public sealed class AllCommentsTagger : ITagger<IOutliningRegionTag>
    {
        private readonly RepeatedSlashCommentOutlineTagger repeatedSlashTagger;
        private readonly SlashStarCommentOutlineTagger slashStarTagger;

        public AllCommentsTagger(ITextBuffer buffer)
        {
            this.repeatedSlashTagger = new RepeatedSlashCommentOutlineTagger(buffer);
            this.slashStarTagger = new SlashStarCommentOutlineTagger(buffer);
        }

#pragma warning disable CS0067 // Event not called - This is a wrapper. The child instances call their versions
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
#pragma warning restore CS0067

        public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            var result = new List<ITagSpan<IOutliningRegionTag>>();

            result.AddRange(this.repeatedSlashTagger.GetTags(spans));
            result.AddRange(this.slashStarTagger.GetTags(spans));

            return result;
        }
    }
}
