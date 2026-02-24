// FileDownload.jslib
// Place this file in: Assets/Plugins/WebGL/FileDownload.jslib
//
// Enables downloading files from Unity WebGL builds
// Used by CSVExporter.cs to trigger browser downloads

mergeInto(LibraryManager.library, {
    
    // Download a text file (CSV, JSON, TXT, etc.)
    DownloadFile: function(filenamePtr, contentPtr) {
        // Convert Unity string pointers to JavaScript strings
        var filename = UTF8ToString(filenamePtr);
        var content = UTF8ToString(contentPtr);
        
        // Create a Blob with the content
        var blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
        
        // Create download URL
        var url = URL.createObjectURL(blob);
        
        // Create temporary link element
        var link = document.createElement('a');
        link.href = url;
        link.download = filename;
        
        // Append to body, click, and remove
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        
        // Clean up the URL object
        URL.revokeObjectURL(url);
        
        console.log('Download triggered: ' + filename);
    },
    
    // Download binary data (for future use - images, etc.)
    DownloadBinaryFile: function(filenamePtr, dataPtr, dataLength) {
        var filename = UTF8ToString(filenamePtr);
        var data = new Uint8Array(dataLength);
        
        for (var i = 0; i < dataLength; i++) {
            data[i] = HEAPU8[dataPtr + i];
        }
        
        var blob = new Blob([data], { type: 'application/octet-stream' });
        var url = URL.createObjectURL(blob);
        
        var link = document.createElement('a');
        link.href = url;
        link.download = filename;
        
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        
        URL.revokeObjectURL(url);
    }
    
});
