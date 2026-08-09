using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using NpcTrackerMod.Core;

namespace NpcTrackerMod.Multiplayer
{
    /// <summary>
    /// Сетевая обёртка снапшота маршрутов: версия формата + gzip-сжатый JSON.
    /// Полный снимок маршрутов в виде JSON может превысить лимит размера пакета
    /// игры (524288 байт), поэтому перед отправкой данные сжимаются — тайлы
    /// маршрутов сильно избыточны, сжатие даёт многократное уменьшение.
    /// </summary>
    public class RouteSyncEnvelope
    {
        /// <summary> Версия формата сообщения. </summary>
        public int Version { get; set; }

        /// <summary> gzip-сжатый JSON снапшота (RouteSnapshot). </summary>
        public byte[] Data { get; set; }

        /// <summary> Сжимает снапшот в сообщение для отправки. </summary>
        public static RouteSyncEnvelope Pack(RouteSnapshot snapshot)
        {
            string json = JsonConvert.SerializeObject(snapshot);
            byte[] raw = Encoding.UTF8.GetBytes(json);

            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
                    gzip.Write(raw, 0, raw.Length);

                return new RouteSyncEnvelope
                {
                    Version = RouteSnapshot.CurrentVersion,
                    Data = output.ToArray()
                };
            }
        }

        /// <summary>
        /// Распаковывает и десериализует сообщение.
        /// Возвращает false при несовместимой версии, повреждённых или пустых данных.
        /// </summary>
        public static bool TryUnpack(RouteSyncEnvelope envelope, out RouteSnapshot snapshot)
        {
            snapshot = null;
            if (envelope == null ||
                envelope.Version != RouteSnapshot.CurrentVersion ||
                envelope.Data == null ||
                envelope.Data.Length == 0)
            {
                return false;
            }

            try
            {
                using (var input = new MemoryStream(envelope.Data))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var reader = new StreamReader(gzip))
                {
                    string json = reader.ReadToEnd();
                    snapshot = JsonConvert.DeserializeObject<RouteSnapshot>(json);
                }
                return snapshot != null;
            }
            catch
            {
                snapshot = null;
                return false;
            }
        }
    }
}
