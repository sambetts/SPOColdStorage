using Microsoft.EntityFrameworkCore;
using SPO.ColdStorage.Entities.DBEntities;
using SPO.ColdStorage.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPO.ColdStorage.Entities
{
    public static class DbExtentions
    {

        public static async Task<SPFile> GetDbFileForFileInfo(this BaseSharePointFileInfo fileMigrated, SPOColdStorageDbContext db)
        {
            // Find/create web & site
            var fileSite = await db.Sites
                .Where(f => f.Url.ToLower() == fileMigrated.SiteUrl.ToLower()).FirstOrDefaultAsync();
            if (fileSite == null)
            {
                fileSite = new Site
                {
                    Url = fileMigrated.SiteUrl.ToLower()
                };
                db.Sites.Append(fileSite);
            }

            var fileWeb = await db.Webs.Where(f => f.Url.ToLower() == fileMigrated.WebUrl.ToLower()).FirstOrDefaultAsync();
            if (fileWeb == null)
            {
                fileWeb = new Web
                {
                    Url = fileMigrated.WebUrl.ToLower(),
                    Site = fileSite
                };
                db.Webs.Append(fileWeb);
            }

            var author = await db.Users.Where(u => u.Email.ToLower() == fileMigrated.Author.ToLower()).SingleOrDefaultAsync();
            if (author == null)
            {
                author = new User { Email = fileMigrated.Author.ToLower() };
                db.Users.Append(author);
            }

            // Find/create file
            var migratedFileRecord = await db.Files.Where(f => f.Url.ToLower() == fileMigrated.FullSharePointUrl.ToLower()).FirstOrDefaultAsync();
            if (migratedFileRecord == null)
            {
                migratedFileRecord = new SPFile
                {
                    Url = fileMigrated.FullSharePointUrl.ToLower(),
                    Web = fileWeb
                };
                db.Files.Append(migratedFileRecord);
            }
            migratedFileRecord.LastModified = fileMigrated.LastModified;
            migratedFileRecord.LastModifiedBy = author;

            return migratedFileRecord;
        }

    }
}
