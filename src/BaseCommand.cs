﻿// Copyright (c) Matt Lacey Ltd. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.TextManager.Interop;

namespace CollapseComments
{
    internal class BaseCommand
    {
        public static readonly Guid CommandSet = new Guid("fafe8ebd-e623-491e-8e27-5543153918c8");

#pragma warning disable SA1401 // Fields should be private
        protected CollapseCommandPackage package;
#pragma warning restore SA1401 // Fields should be private

        protected Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider => this.package;

        protected async System.Threading.Tasks.Task ExecuteAsync(Mode mode)
        {
            IVsTextManager txtMgr = (IVsTextManager)await this.ServiceProvider.GetServiceAsync(typeof(SVsTextManager));
            if (txtMgr == null)
            {
                throw new ArgumentNullException(nameof(txtMgr));
            }

            int mustHaveFocus = 1;
            txtMgr.GetActiveView(mustHaveFocus, null, out var vTextView);
            if (!(vTextView is IVsUserData userData))
            {
                Console.WriteLine("No text view is currently open");
                return;
            }

            var guidViewHost = Microsoft.VisualStudio.Editor.DefGuidList.guidIWpfTextViewHost;
            userData.GetData(ref guidViewHost, out var holder);
            var viewHost = (IWpfTextViewHost)holder;

            var componentModel = (IComponentModel)await this.ServiceProvider.GetServiceAsync(typeof(SComponentModel));
            var outliningManagerService = componentModel?.GetService<IOutliningManagerService>();

            var mgr = outliningManagerService?.GetOutliningManager(viewHost.TextView);

            var regions = mgr?.GetAllRegions(new SnapshotSpan(viewHost.TextView.TextSnapshot, 0, viewHost.TextView.TextSnapshot.Length));

            var includeDirectives = this.package.Options.IncludeUsingDirectives;

            bool IsComment(string collapsedText)
            {
                return collapsedText.StartsWith("/")
                    || collapsedText.StartsWith("'")
                    || collapsedText.StartsWith("<!--");
            }

            bool IsUsing(string collapsedText)
            {
                return collapsedText.Contains("\r\nusing ")
                    || collapsedText.Contains("\r\nImports");
            }

            if (regions != null)
            {
                foreach (var region in regions)
                {
                    if (!region.IsCollapsible)
                    {
                        continue;
                    }

                    if (mode == Mode.CollapseComments && region.IsCollapsed)
                    {
                        // CollapseComments doesn't change any non-comment regions.
                        continue;
                    }

                    var collapsedText = region.CollapsedForm?.ToString();
                    var hiddenText = region.Extent.GetText(region.Extent.TextBuffer.CurrentSnapshot);

                    // Support XMLDoc format comments
                    //  <summary>
                    //  Some details.
                    //  </summary>
                    //
                    // and support generated comments from decompiled code
                    // //
                    // // Summary:
                    // //   Some details.
                    if (IsComment(hiddenText) || IsUsing(hiddenText))
                    {
                        if (IsUsing(hiddenText) && !includeDirectives)
                        {
                            continue;
                        }

                        if (mode == Mode.CollapseComments)
                        {
                            if (!region.IsCollapsed && region.IsCollapsible)
                            {
                                mgr.TryCollapse(region);
                            }
                        }
                        else if (mode == Mode.ExpandComments)
                        {
                            if (region.IsCollapsed && region is ICollapsed collapsed)
                            {
                                mgr.Expand(collapsed);
                            }
                        }
                    }
                    else
                    {
                        if (mode == Mode.ExpandComments)
                        {
                            var visibleText = region.Extent.GetStartPoint(region.Extent.TextBuffer.CurrentSnapshot).GetContainingLine().GetText();

                            // Don't collapse higher level elements.
                            if (!visibleText.ContainsFollowedByWhitespaceInsensitive("#region")
                             && !visibleText.ContainsFollowedByWhitespaceInsensitive("namespace")
                             && !visibleText.ContainsSurroundedByWhitespaceInsensitive("class")
                             && !visibleText.ContainsSurroundedByWhitespaceInsensitive("enum")
                             && !visibleText.ContainsSurroundedByWhitespaceInsensitive("struct")
                             && !visibleText.ContainsSurroundedByWhitespaceInsensitive("Module")
                             && !region.IsCollapsed && region.IsCollapsible)
                            {
                                mgr.TryCollapse(region);
                            }
                        }
                    }
                }
            }
        }
    }
}
