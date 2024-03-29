﻿using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace ModFinder.Util
{
  internal static class HttpHelper
  {
    private static readonly HttpClient _httpClient = new HttpClient();

    public static async Task DownloadFileAsync(string uri, string outputPath)
    {
      Uri uriResult;

      if (!Uri.TryCreate(uri, UriKind.Absolute, out uriResult))
        throw new InvalidOperationException("URI is invalid.");

      if (!File.Exists(outputPath))
        throw new FileNotFoundException("File not found.", nameof(outputPath));

      byte[] fileBytes = await _httpClient.GetByteArrayAsync(uri);
      File.WriteAllBytes(outputPath, fileBytes);
    }

    public static string GetResponseContent(string url)
    {
      using HttpResponseMessage response = _httpClient.GetAsync(url).Result;
      using HttpContent content = response.Content;
      return content.ReadAsStringAsync().Result;
    }
  }
}