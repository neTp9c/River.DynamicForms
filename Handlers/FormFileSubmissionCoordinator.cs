using Orchard.DynamicForms.Services;
using Orchard.DynamicForms.Services.Models;
using Orchard.Environment.Extensions;
using River.DynamicForms.Elements;
using System.Collections.Generic;
using System.IO;
using Orchard.Layouts.Helpers;
using Orchard.MediaLibrary.Services;
using Orchard.Tokens;
using Orchard.ContentManagement;
using System;
using Orchard.MediaLibrary.Models;
using System.Web;
using Orchard.FileSystems.Media;

namespace River.DynamicForms.Handlers
{
    [OrchardFeature("River.DynamicForms.Elements.FileField")]
    public class FormFileSubmissionCoordinator : FormEventHandlerBase
    {
        private readonly IMediaLibraryService _mediaLibraryService;
        private readonly IStorageProvider _storageProvider;
        private readonly ITokenizer _tokenizer;
        private readonly IContentManager _contentManager;

        public FormFileSubmissionCoordinator(
            IMediaLibraryService mediaLibraryService,
            IStorageProvider storageProvider,
            ITokenizer tokenizer,
            IContentManager contentManager)
        {
            _mediaLibraryService = mediaLibraryService;
            _storageProvider = storageProvider;
            _tokenizer = tokenizer;
            _contentManager = contentManager;
        }

        public override void Submitted(FormSubmittedEventContext context)
        {
            foreach (var element in context.Form.Elements.Flatten())
            {
                var fileFieldElement = element as FileField;
                if (fileFieldElement == null)
                    continue;

                var postedFileValue = context.ValueProvider.GetValue(fileFieldElement.Name);
                if (postedFileValue == null)
                    continue;

                var postedFiles = (HttpPostedFileBase[])postedFileValue.RawValue;
                if (postedFiles == null && postedFiles.Length != 1)
                    continue;

                var folderPath = _tokenizer.Replace(fileFieldElement.FilePath, new { });
                var uniqFileName = _mediaLibraryService.GetUniqueFilename(folderPath, postedFiles[0].FileName);
                var path = _storageProvider.Combine(fileFieldElement.FilePath, uniqFileName);

                context.Values[fileFieldElement.Name + ":size"] = postedFiles[0].ContentLength.ToString();
                context.Values[fileFieldElement.Name] = path;
                fileFieldElement.PostedValue = path;
            }
        }

        public override void Validated(FormValidatedEventContext context)
        {
            if (!context.ModelState.IsValid)
            {
                //Clean up on validation fail
                foreach (var element in context.Form.Elements.Flatten())
                {
                    if (element is FileField)
                    {
                        var fileFieldElement = element as FileField;
                        if (fileFieldElement == null)
                            continue;

                        var postedFileValue = context.ValueProvider.GetValue(fileFieldElement.Name);
                        if (postedFileValue == null)
                            continue;

                        var postedFiles = (HttpPostedFileBase[])postedFileValue.RawValue;
                        if (postedFiles == null && postedFiles.Length != 1)
                            continue;

                        var filePath = context.Values[fileFieldElement.Name];
                        if (string.IsNullOrWhiteSpace(filePath))
                            continue;

                        _mediaLibraryService.DeleteFile(
                            Path.GetDirectoryName(filePath),
                            Path.GetFileName(filePath));
                    }
                }
            }
            else
            {
                foreach (var element in context.Form.Elements.Flatten())
                {
                    var fileFieldElement = element as FileField;
                    if (fileFieldElement == null)
                        continue;

                    var postedFileValue = context.ValueProvider.GetValue(fileFieldElement.Name);
                    if (postedFileValue == null)
                        continue;

                    var postedFiles = (HttpPostedFileBase[])postedFileValue.RawValue;
                    if (postedFiles == null && postedFiles.Length != 1)
                        continue;

                    var filePath = context.Values[fileFieldElement.Name];
                    if (string.IsNullOrWhiteSpace(filePath))
                        continue;

                    var mediaPart = _mediaLibraryService.ImportMedia(
                        Path.GetDirectoryName(filePath),
                        Path.GetFileName(filePath));
                    _contentManager.Create(mediaPart);
                } 
            }
        }

        public override void Validating(FormValidatingEventContext context)
        {
            foreach (var element in context.Form.Elements.Flatten())
            {
                var fileFieldElement = element as FileField;
                if (fileFieldElement == null)
                    continue;

                var postedFileValue = context.ValueProvider.GetValue(fileFieldElement.Name);
                if (postedFileValue == null)
                    continue;

                var postedFiles = (HttpPostedFileBase[])postedFileValue.RawValue;
                if (postedFiles == null && postedFiles.Length != 1)
                    continue;

                var filePath = context.Values[fileFieldElement.Name];
                if (string.IsNullOrWhiteSpace(filePath))
                    continue;

                try
                {
                    _mediaLibraryService.UploadMediaFile(
                        Path.GetDirectoryName(filePath),
                        Path.GetFileName(filePath),
                        postedFiles[0].InputStream);
                }
                catch
                {
                    context.ModelState.AddModelError(fileFieldElement.Name, "Error Saving File");
                }
            }
        }
    }
}
