﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using ArizaApp.Helpers;
using ArizaApp.Models.DbContexts;
using ArizaApp.Models.Entities;
using ArizaApp.Models.Options;
using ArizaApp.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ArizaApp.Services
{
    public class FileUploadService : IFileUploadService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ArizaDbContext _dbContext;
        private readonly ServerOptions _serverOptions;

        public FileUploadService(IOptions<ServerOptions> serverOptions, ArizaDbContext dbContext, IWebHostEnvironment env)
        {
            _dbContext = dbContext;
            _env = env;
            _serverOptions = serverOptions.Value;
        }

        public async Task<bool> UploadFileAsync(IList<IFormFile> files, int notificationId, AppUser user)
        {
            foreach (var formFile in files)
            {
                var mediaPath = Path.Combine(_env.WebRootPath, "Media");
                
                if(!Directory.Exists(mediaPath))
                    Directory.CreateDirectory(mediaPath);
                
                var fileSize = GeneralExtensions.CalculateFileSize(formFile.Length);

                var newFileName = $"{formFile.Name}_{DateTime.Now.FullDateAndTimeStringWithUnderscore()}" +
                                  Path.GetExtension(formFile.FileName);

                if (formFile.Length <= 0) continue;

                var filePath = Path.Combine(mediaPath, newFileName);

                if (!string.IsNullOrEmpty(newFileName) && !string.IsNullOrEmpty(fileSize))
                    _dbContext.UploadedFileRecords.Add(new UploadedFileRecords
                    {
                        NotificationId = notificationId,
                        User = user,
                        FileName = newFileName,
                        FileSize = fileSize,
                        FilePath = filePath,
                        UploadDate = DateTime.Now,
                        FileExtension = Path.GetExtension(formFile.FileName)
                    });

                await using var stream = File.Create(filePath);
                await formFile.CopyToAsync(stream);

                var WebClient = new WebClient();
                WebClient.Credentials = new NetworkCredential(_serverOptions.NetworkUserName, _serverOptions.NetworkUserPassword);
                var uri = new Uri(_serverOptions.RemoteUploadFileFullPath);
                WebClient.UploadFile(uri, "POST", filePath);
            }

            var result = await _dbContext.SaveChangesAsync();

            return result > 0;
        }
    }
}