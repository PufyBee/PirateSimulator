// FileDownload.jslib
// Place this file in: Assets/Plugins/WebGL/
// 
// Provides a DownloadFile function that Unity WebGL can call to trigger
// an actual browser download of file content (CSV, text, etc).
//
// The function name "DownloadFile" must exactly match the [DllImport]
// declaration in CSVExporter.cs

mergeInto(LibraryManager.library, {

    DownloadFile: function (filenamePtr, contentPtr) {
        // Convert Unity string pointers to JavaScript strings
        var filename = UTF8ToString(filenamePtr);
        var content = UTF8ToString(contentPtr);

        try {
            // Create a Blob with the file content
            // Use text/csv MIME type for CSV files (works for any text content too)
            var blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });

            // Create a temporary download URL
            var url = URL.createObjectURL(blob);

            // Create a hidden anchor element to trigger the download
            var a = document.createElement('a');
            a.style.display = 'none';
            a.href = url;
            a.download = filename;

            // Append to document, click it, then remove it
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);

            // Free the temporary URL after a brief delay
            setTimeout(function () {
                URL.revokeObjectURL(url);
            }, 100);

            console.log('CSV download triggered: ' + filename);
        } catch (e) {
            console.error('CSV download failed: ' + e.message);
        }
    }

});
