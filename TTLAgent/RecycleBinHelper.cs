using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using Shell32;

namespace TTLAgent
{
    internal static class RecycleBinHelper
    {
        [STAThread()]
        public static void DeleteOldFiles(DateTime maxFileAge)
        {
            // create shell
            var shell = new Shell();

            // get recycler folder
            var recyclerFolder = shell.NameSpace(10);
            int rfDeleted = 0;
            // for each files
            for (int i = 0; i < recyclerFolder.Items().Count; i++)
            {
                // get the folder item
                var folderItem = recyclerFolder.Items().Item(i);

                // get file name
                var filename = recyclerFolder.GetDetailsOf(folderItem, 0);
                var myDate = CleanString(recyclerFolder.GetDetailsOf(folderItem, 2));
                //var x = myDate.Substring(0, 1);
                DateTime dateDeleted;
                if (!DateTime.TryParse(myDate, out dateDeleted))
                {
                    Program.PrintConsole($"Can't parse {myDate} as valid date.");
                    continue;
                }
                Console.WriteLine($"{filename}\t{dateDeleted.ToString("G")}");
                if (dateDeleted < maxFileAge)
                {//Delete this file
                    //recyclerFolder.Items(i)
                    rfDeleted++;
                }
            }
            if (rfDeleted > 0) Program.PrintConsole($"Deleted {rfDeleted} files from Recycle Bin.", MessageType.Success);
        }

        private static string CleanString(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            const string goodChars = "0123456789./: ";
            foreach (char c in s.Where(x => goodChars.Contains(x)))
            {
                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
//[0] = Name
//[1] = Size
//[2] = Item type
//[3] = Date modified
//[4] = Date created
//[5] = Date accessed
//[6] = Attributes
//[7] = Offline status
//[8] = Availability
//[9] = Perceived type
//[10] = Owner
//[11] = Kind
//[12] = Date taken
//[13] = Contributing artists
//[14] = Album
//[15] = Year
//[16] = Genre
//[17] = Conductors
//[18] = Tags
//[19] = Rating
//[20] = Authors
//[21] = Title
//[22] = Subject
//[23] = Categories
//[24] = Comments
//[25] = Copyright
//[26] = #
//[27] = Length
//[28] = Bit rate
//[29] = Protected
//[30] = Camera model
//[31] = Dimensions
//[32] = Camera maker
//[33] = Company
//[34] = File description
//[35] = Program name
//[36] = Duration
//[37] = Is online
//[38] = Is recurring
//[39] = Location
//[40] = Optional attendee addresses
//[41] = Optional attendees
//[42] = Organizer address
//[43] = Organizer name
//[44] = Reminder time
//[45] = Required attendee addresses
//[46] = Required attendees
//[47] = Resources
//[48] = Meeting status
//[49] = Free/busy status
//[50] = Total size
//[51] = Account name