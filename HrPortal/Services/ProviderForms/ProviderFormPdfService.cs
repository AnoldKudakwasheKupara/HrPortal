using HrPortal.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HrPortal.Services.ProviderForms
{
    /// <summary>
    /// Fills in a provider's own form. The answers are drawn on an
    /// empty see-through page and that page is laid over the provider's
    /// file, so the form itself is carried across as it came and the
    /// provider gets their own document back.
    /// </summary>
    public class ProviderFormPdfService
    {
        private const string Ink = "#14213D";

        private const float TextSize = 9.5f;

        private static readonly string Tick =
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 12 10'>" +
            $"<path d='M1.5 5.2 L4.6 8.4 L10.5 1.4' fill='none' stroke='{Ink}' " +
            "stroke-width='1.7' stroke-linecap='round'/></svg>";

        /// <summary>The provider's blank form, exactly as they issued it.</summary>
        public static byte[] Blank(ProviderFormKind kind)
        {
            var name = $"ProviderForms.{kind.ToString().ToLowerInvariant()}.pdf";

            using var stream = typeof(ProviderFormPdfService).Assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"The blank form {name} is missing from the build.");

            using var copy = new MemoryStream();
            stream.CopyTo(copy);

            return copy.ToArray();
        }

        public byte[] Render(ProviderForm form)
        {
            var folder = Directory.CreateTempSubdirectory("provider-form-");

            try
            {
                var blank = Path.Combine(folder.FullName, "blank.pdf");
                var answers = Path.Combine(folder.FullName, "answers.pdf");
                var filled = Path.Combine(folder.FullName, "filled.pdf");

                File.WriteAllBytes(blank, Blank(form.Kind));
                Answers(form).GeneratePdf(answers);

                DocumentOperation
                    .LoadFile(blank)
                    .OverlayFile(new DocumentOperation.LayerConfiguration { FilePath = answers })
                    .Save(filled);

                return File.ReadAllBytes(filled);
            }
            finally
            {
                folder.Delete(true);
            }
        }

        private static IDocument Answers(ProviderForm form)
        {
            var (width, height) = ProviderFormLayout.PageSize(form.Kind);
            var marks = ProviderFormLayout.Marks(form).ToLookup(m => m.Page);

            return Document.Create(document =>
            {
                for (var number = 0; number < ProviderFormLayout.Pages(form.Kind); number++)
                {
                    var page = marks[number].ToList();

                    document.Page(sheet =>
                    {
                        sheet.Size(width, height);
                        sheet.Margin(0);
                        sheet.PageColor(Colors.Transparent);

                        sheet.Content().Layers(layers =>
                        {
                            layers.PrimaryLayer().Extend();

                            foreach (var mark in page)
                                Draw(layers.Layer(), mark);
                        });
                    });
                }
            });
        }

        private static void Draw(IContainer layer, FormMark mark)
        {
            var box = layer
                .PaddingLeft(mark.X)
                .PaddingTop(mark.Y - mark.Height)
                .Width(mark.Width)
                .Height(mark.Height);

            if (mark.IsTick)
            {
                box.AlignCenter().AlignMiddle().Width(8.4f).Height(7f).Svg(Tick).FitArea();
            }
            else if (mark.Image != null)
            {
                box.AlignBottom().AlignLeft().Image(mark.Image).FitArea();
            }
            else
            {
                box.AlignBottom().ScaleToFit().Text(mark.Text!)
                    .FontFamily(Fonts.Lato)
                    .FontSize(Math.Min(TextSize, mark.Height * 0.95f))
                    .LineHeight(1)
                    .FontColor(Ink);
            }
        }

        /// <summary>A download name the provider and HR can both tell apart.</summary>
        public static string FileName(ProviderForm form)
        {
            var who = $"{form.Member.FirstNames} {form.Member.Surname}".Trim();

            foreach (var bad in Path.GetInvalidFileNameChars())
                who = who.Replace(bad, ' ');

            return $"{ProviderFormRules.Title(form.Kind)} - {who} - {form.Reference}.pdf";
        }
    }
}
