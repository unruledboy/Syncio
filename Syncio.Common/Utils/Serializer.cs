using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;

namespace Syncio.Common.Utils
{
	public class Serializer
	{
		public static string SerializeText(object data)
		{
			return JsonConvert.SerializeObject(data);
		}

		public static T DeserializeText<T>(string text)
		{
			return JsonConvert.DeserializeObject<T>(text);
		}

		public static byte[] Serialize(object data)
		{
			var text = JsonConvert.SerializeObject(data);
			using (var outStream = new MemoryStream())
			{
				using (var tinyStream = new GZipStream(outStream, CompressionMode.Compress))
				using (var mStream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
					mStream.CopyTo(tinyStream);

				return outStream.ToArray();
			}
		}

		public static T Deserialize<T>(byte[] data)
		{
			using (var inStream = new MemoryStream(data))
			using (var bigStream = new GZipStream(inStream, CompressionMode.Decompress))
			using (var bigStreamOut = new MemoryStream())
			{
				bigStream.CopyTo(bigStreamOut);
				var text = Encoding.UTF8.GetString(bigStreamOut.ToArray());
				return JsonConvert.DeserializeObject<T>(text);
			}
		}
	}
}
