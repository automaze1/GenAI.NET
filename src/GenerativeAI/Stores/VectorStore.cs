using Automation.GenerativeAI.Interfaces;
using Automation.GenerativeAI.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Automation.GenerativeAI.Stores
{
    class MatchedObject : IMatchedObject
    {
        public double Score { get; set; }
        public IDictionary<string, string> Attributes { get; set; }
    }

    [Serializable]
    public class VectorStore : IVectorStore
    {
        private ConcurrentBag<double[]> vectors = new ConcurrentBag<double[]>();
        //private List<double[]> vectors = new List<double[]>();
        //private List<IDictionary<string, string>> attributes = new List<IDictionary<string, string>>();
        private ConcurrentBag<IDictionary<string,string>> attributes = new ConcurrentBag<IDictionary<string, string>>();

        private IVectorTransformer transformer = null;
        private readonly string v1header = "Automation.Classifier.VectorStore v1.0";
        private VectorStore() { }

        /// <summary>
        /// Vector Store constructor
        /// </summary>
        /// <param name="transformer">Vector transformer</param>
        public VectorStore(IVectorTransformer transformer)
        {
            this.transformer = transformer;
        }

        public int VectorLength => transformer.VectorLength;

        public void Add(double[] vector, IDictionary<string, string> attributes)
        {
            vectors.Add(vector);
            this.attributes.Add(attributes);
        }

        private static IDictionary<string, string> ToDictionary(ITextObject text, bool savetext, int index)
        {
            var dict = new Dictionary<string, string>();
            dict.Add("Name", text.Name);
            dict.Add("Class", text.Class);
            dict.Add("Index", index.ToString());
            if (savetext) { dict.Add("Text", text.Text); }
            return dict;
        }

        public void Add(IEnumerable<ITextObject> textObjects, bool savetext)
        {
            var validtxts = textObjects.Where(x => !string.IsNullOrEmpty(x.Text));
            int i = 0;
            Parallel.ForEach(validtxts, txt =>
            {
                var vec = transformer.Transform(txt.Text);
                vectors.Add(vec);
                Interlocked.Increment(ref i);
                var dict = ToDictionary(txt, savetext, i);
                attributes.Add(dict);
            });
        }

        public IEnumerable<IMatchedObject> Search(double[] vector, int resultcount)
        {
            if(resultcount > vectors.Count)
            {
                resultcount = vectors.Count;
            }

            ConcurrentBag<IMatchedObject> results = new ConcurrentBag<IMatchedObject>();

            Parallel.For(0, vectors.Count, idx =>
            {
                var match = new MatchedObject() { Attributes = attributes.ElementAt(idx) };
                var vec = vectors.ElementAt(idx);
                match.Score = 1 - vec.CosineDistance(vector);
                results.Add(match);
            });

            return results.OrderByDescending(t => t.Score).Take(resultcount);
        }

        public IEnumerable<IMatchedObject> Search(ITextObject textObject, int resultcount)
        {
            return Search(transformer.Transform(textObject.Text), resultcount);
        }

        public static VectorStore Create(string recepiefile)
        {
            var ext = Path.GetExtension(recepiefile);
            if (ext.Contains("vdb", StringComparison.CurrentCultureIgnoreCase))
            {
                recepiefile = Path.ChangeExtension(recepiefile, "vdb");
            }

            var store = new VectorStore();
            using (var stream = new FileStream(recepiefile, FileMode.Open))
            {
                using (var headerreader = new BinaryReader(stream))
                {
                    var header = headerreader.ReadString();
                    if (string.Compare(header, store.v1header) != 0)
                    {
                        var error = "Invalid Vector Store model file format, header info is missing";
                        Logger.WriteLog(LogLevel.Error, LogOps.Result, error);
                        throw new FormatException(error);
                    }

                    using (var gzip = new GZipStream(stream, CompressionMode.Decompress, true))
                    {
                        using(var reader = new BinaryReader(gzip))
                        {
                            int nVectors = reader.ReadInt32();
                            int nVectorLength = 0;
                            for (int i = 0; i < nVectors; ++i)
                            {
                                nVectorLength = reader.ReadInt32();
                                var vector = new double[nVectorLength];
                                for (int j = 0; j < nVectorLength; ++j)
                                {
                                    vector[j] = reader.ReadDouble();
                                }
                                store.vectors.Add(vector);
                            }
                            int nAttributes = reader.ReadInt32();
                            for (int k = 0; k < nAttributes; k++)
                            {
                                int n = reader.ReadInt32();
                                var attribute = new Dictionary<string, string>();
                                for (int j = 0; j < n; ++j)
                                {
                                    string key = reader.ReadString();
                                    string value = reader.ReadString();
                                    attribute.Add(key, value);
                                }
                                store.attributes.Add(attribute);
                            }

                            try
                            {
                                var classname = reader.ReadString();
                                var type = Type.GetType(classname);
                                store.transformer = (IVectorTransformer)Activator.CreateInstance(type);
                            }
                            catch (Exception ex)
                            {
                                if (nVectorLength == 1536)
                                {
                                    Logger.WriteLog(LogLevel.Warning, LogOps.Exception, $"Couldn't deserialize vector transformer, creating a default transformer. Exception: {ex.Message}");
                                    store.transformer = new OpenAIEmbeddingTransformer();
                                }
                                else
                                {
                                    throw;
                                }
                            }
                        }
                    }
                }
            }

            return store;
        }

        public void Save(string filepath)
        {
            using (var stream = new FileStream(filepath, FileMode.Create))
            {
                using(var headerwriter = new BinaryWriter(stream))
                {
                    //Write the header info
                    headerwriter.Write(v1header);

                    using (var gzip = new GZipStream(stream, CompressionMode.Compress, true))
                    {
                        using (var writer = new BinaryWriter(gzip))
                        {
                            //Serialize Vectors
                            writer.Write(this.vectors.Count);
                            foreach (var item in vectors)
                            {
                                writer.Write(item.Length);
                                for (int i = 0; i < item.Length; i++)
                                {
                                    writer.Write(item[i]);
                                }
                            }

                            //Serialize attributes
                            writer.Write(attributes.Count);
                            foreach (var att in attributes)
                            {
                                writer.Write(att.Count);
                                foreach (var a in att)
                                {
                                    writer.Write(a.Key);
                                    writer.Write(a.Value);
                                }
                            }

                            //Serialize vector transformer class so that it can be instatiated.
                            //TODO: what if the transformer can't be instantiated with class name.
                            var transformerclass = transformer.GetType().FullName;
                            writer.Write(transformerclass);
                        }
                    }
                }
            }
        }
    }
}
