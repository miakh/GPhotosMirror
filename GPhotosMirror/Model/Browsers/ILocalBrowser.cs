using System.Threading.Tasks;

namespace GPhotosMirror.Model.Browsers
{
    public interface ILocalBrowser
    {
        public string BrowserID { get; }
        public Task<string> GetExecutable();
    }
}
