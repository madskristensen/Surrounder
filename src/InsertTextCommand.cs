using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Surrounder
{
    [Export(typeof(ICommandHandler))]
    [Name(nameof(InsertTextCommand))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    internal class InsertTextCommand : ICommandHandler<TypeCharCommandArgs>
    {
        [Import]
        internal ITextUndoHistoryRegistry TextUndoHistoryRegistry = null; // Set via MEF

        private static readonly Dictionary<char, string> _pairs = new()
        {
            { '\'', "'"},
            { '"', "\""},
            { '(', ")"},
            { '{', "}"},
            { '[', "]"},
            { '<', ">"},
            { '`', "`"},
        };

        public string DisplayName => nameof(InsertTextCommand);

        public bool ExecuteCommand(TypeCharCommandArgs args, CommandExecutionContext executionContext)
        {
            // Cheap checks to see if we should continue
            if (!_pairs.ContainsKey(args.TypedChar) || // Type char is valid token
                args.TextView.Selection.IsEmpty || // Selection isn't empty
                args.TextView.Selection.SelectedSpans.Count > 1) // Not multi-selection
            {
                return false;
            }

            // More expensive checks to see if we should continue 
            string selectedText = args.TextView.TextSnapshot.GetText(args.TextView.Selection.SelectedSpans[0].Span);

            if (string.IsNullOrWhiteSpace(selectedText)) // Don't execute when the selection only contains whitespace
            {
                return false;
            }

            // At this point we should execute our logic
            ITextUndoHistory undoHistory = TextUndoHistoryRegistry.RegisterHistory(args.TextView.TextBuffer);

            using (ITextUndoTransaction transaction = undoHistory.CreateTransaction("Surround Selection"))
            {
                int start = args.TextView.Selection.SelectedSpans[0].Start.Position;
                int length = args.TextView.Selection.SelectedSpans[0].Length;

                // 1. Insert the character pairs
                using (ITextEdit edit = args.TextView.TextBuffer.CreateEdit())
                {
                    edit.Insert(start + length, _pairs[args.TypedChar]);
                    edit.Insert(start, args.TypedChar.ToString());
                    edit.Apply();
                }

                // 2. Re-set the selection
                SnapshotSpan newSelection = new(args.TextView.TextSnapshot, start + 1, length);
                args.TextView.Selection.Select(newSelection, false);

                // 3. Move the caret to its new position
                SnapshotPoint newCaretPosition = new(args.TextView.TextSnapshot, start + length + 1);
                args.TextView.Caret.MoveTo(newCaretPosition);

                transaction.Complete();
            }

            return true; // returning true means we handled the command successfully
        }

        public CommandState GetCommandState(TypeCharCommandArgs args)
        {
            return CommandState.Available;
        }
    }
}
