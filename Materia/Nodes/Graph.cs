﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Reflection;
using System.Windows;
using Materia.Nodes.Atomic;
using Materia.Nodes.Attributes;
using OpenTK.Graphics.OpenGL;
using Materia.MathHelpers;
using System.Threading;

namespace Materia.Nodes
{
    public enum GraphPixelType
    {
        RGBA = PixelInternalFormat.Rgba8,
        RGBA16F = PixelInternalFormat.Rgba16f,
        RGBA32F = PixelInternalFormat.Rgba32f,
        RGB = PixelInternalFormat.Rgb8,
        RGB16F = PixelInternalFormat.Rgb16f,
        RGB32F = PixelInternalFormat.Rgb32f,
        Luminance16F = PixelInternalFormat.R16f,
        Luminance32F = PixelInternalFormat.R32f,
    }

    public class Graph : IDisposable
    {
        public delegate void GraphUpdate(Graph g);
        public event GraphUpdate OnGraphUpdated;

        [HideProperty]
        public bool ReadOnly { get; set; }
        [HideProperty]
        public string CWD { get; set; }
        public List<Node> Nodes { get; protected set; }
        public Dictionary<string, Node> NodeLookup { get; protected set; }
        public List<string> OutputNodes { get; protected set; }
        public List<string> InputNodes { get; protected set; }

        protected Dictionary<string, object> Variables { get; set; }
        protected Dictionary<string, Point> OriginSizes;

        protected Dictionary<string, Node.NodeData> tempData;

        /// <summary>
        /// Parameters are only available for image graphs and fx graphs
        /// </summary>
        [GraphParameterEditor]
        public Dictionary<string, GraphParameterValue> Parameters { get; protected set; }
        
        [Section(Section = "Custom Parameters")]
        [Title(Title = "")]
        [ParameterEditor]
        public List<GraphParameterValue> CustomParameters { get; protected set; }

        [Section(Section = "Custom Functions")]
        [Title(Title = "")]
        [GraphFunctionEditor]
        public List<FunctionGraph> CustomFunctions { get; protected set; }

        public string Name { get; set; }

        [HideProperty]
        public double ShiftX { get; set; }

        [HideProperty]
        public double ShiftY { get; set; }

        [HideProperty]
        public float Zoom { get; set; }

        protected GraphPixelType defaultTextureType;

        [Dropdown(null)]
        [Title(Title = "Default Texture Type")]
        public GraphPixelType DefaultTextureType
        {
            get
            {
                return defaultTextureType;
            }
            set
            {
                if (!ReadOnly)
                {
                    defaultTextureType = value;
                }
            }
        }

        protected string hdriIndex;

        [HideProperty]
        public string HdriIndex
        {
            get
            {
                return hdriIndex;
            }
            set
            {
                hdriIndex = value;
                Hdri.HdriManager.Selected = value;
            }
        }

        [Dropdown("HdriIndex")]
        [Title(Title = "Hdri Image")]
        public string[] HdriImages { get; set; }

        protected int randomSeed;

        [Title(Title = "Random Seed")]
        public int RandomSeed
        {
            get
            {
                return randomSeed;
            }
            set
            {
                randomSeed = value;
				
				if(this is FunctionGraph)
                {
                    Updated();
                    return;
                }

                Updated();
                TryAndProcess();
            }
        }

        public class GPoint
        {
            public double x;
            public double y;

            public GPoint()
            {

            }

            public GPoint(double x, double y)
            {
                this.x = x;
                this.y = y;
            }

            public GPoint(Point p)
            {
                x = p.X;
                y = p.Y;
            }

            public Point ToPoint()
            {
                return new Point(x, y);
            }
        }

        protected int width;
        protected int height;
        [Slider(IsInt = true, Max = 4096, Min = 8, Snap = true, Ticks = new float[] { 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096 })]
        [Section(Section = "Standard")]
        public int Width
        {
            get
            {
                return width;
            }
            set
            {
                if(!ReadOnly)
                    width = value;
            }
        }

        [Slider(IsInt = true, Max = 4096, Min = 8, Snap = true, Ticks = new float[] { 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096 })]
        [Section(Section = "Standard")]
        public int Height
        {
            get
            {
                return height;
            }
            set
            {
                if(!ReadOnly)
                    height = value;
            }
        }

        public Graph(string name, int w = 256, int h = 256)
        {
            tempData = new Dictionary<string, Node.NodeData>();
            hdriIndex = Hdri.HdriManager.Selected;
            HdriImages = Hdri.HdriManager.Available.ToArray();

            Name = name;
            Zoom = 1;
            ShiftX = ShiftY = 0;
            width = w;
            height = h;

            Variables = new Dictionary<string, object>();
            defaultTextureType = GraphPixelType.RGBA;
            Nodes = new List<Node>();
            NodeLookup = new Dictionary<string, Node>();
            OutputNodes = new List<string>();
            InputNodes = new List<string>();
            OriginSizes = new Dictionary<string, Point>();
            Parameters = new Dictionary<string, GraphParameterValue>();
            CustomParameters = new List<GraphParameterValue>();
            CustomFunctions = new List<FunctionGraph>();
        }

        public virtual object GetVar(string k)
        {
            if (k == null) return null;

            if(Variables.ContainsKey(k))
            {
                return Variables[k];
            }

            return null;
        }

        public Node FindSubNodeById(string id)
        {
            Node n = null;
            var procnodes = Nodes.FindAll(m => m is PixelProcessorNode);

            for(int i = 0; i < procnodes.Count; i++)
            {
                var proc = procnodes[i] as PixelProcessorNode;

                if(proc.Function != null)
                {
                    if(proc.Function.NodeLookup.TryGetValue(id, out n))
                    {
                        return n;
                    }
                }
            }

            foreach(string k in Parameters.Keys)
            {
                var parameter = Parameters[k];
                if(parameter.IsFunction())
                {
                    var g = parameter.Value as FunctionGraph;

                    if(g.NodeLookup.TryGetValue(id, out n))
                    {
                        return n;
                    }
                }
            }

            foreach(FunctionGraph g in CustomFunctions)
            {
                if(g.NodeLookup.TryGetValue(id, out n))
                {
                    return n;
                }
            }

            return null;
        }

        public virtual void RemoveVar(string k)
        {
            Variables.Remove(k);
        }

        public virtual void SetVar(string k, object v)
        {
            Variables[k] = v;
        }

        public class GraphData
        {
            public string name;
            public List<string> nodes;
            public List<string> outputs;
            public List<string> inputs;
            public GraphPixelType defaultTextureType;

            public double shiftX;
            public double shiftY;
            public float zoom;

            public int width;
            public int height;

            public string hdriIndex;

            public Dictionary<string, string> parameters;
            public List<string> customParameters;
            public List<string> customFunctions;
        }

        public virtual void TryAndProcess()
        {
            int c = Nodes.Count;
            for(int i = 0; i < c; i++)
            {
                Node n = Nodes[i];

                if (OutputNodes.Contains(n.Id))
                {
                    continue;
                }
                if (InputNodes.Contains(n.Id))
                {
                    continue;
                }

                n.TryAndProcess();
            }

            c = InputNodes.Count;

            for(int i = 0; i < c; i++)
            {
                Node n;
                if (NodeLookup.TryGetValue(InputNodes[i], out n))
                {
                    InputNode inp = (InputNode)n;
                    inp.TryAndProcess();
                }
            }
        }

        /// <summary>
        /// this is used in GraphInstances
        /// To resize proportionate to new size
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public virtual void ResizeWith(int width, int height)
        {
            int c = Nodes.Count;

            float wp = (float)width / (float)this.width;
            float hp = (float)height / (float)this.height;

            for (int i = 0; i < c; i++)
            {
                Node n = Nodes[i];

                if (OutputNodes.Contains(n.Id))
                {
                    continue;
                }
                if (InputNodes.Contains(n.Id))
                {
                    continue;
                }

                if (n is BitmapNode)
                {
                    BitmapNode bn = (BitmapNode)n;
                    bn.TryAndProcess();
                }
                else
                {
                    Point osize;

                    if (OriginSizes.TryGetValue(n.Id, out osize))
                    {
                        int fwidth = (int)Math.Min(4096, Math.Max(8, Math.Round(osize.X * wp)));
                        int fheight = (int)Math.Min(4096, Math.Max(8, Math.Round(osize.Y * hp)));

                        n.Width = fwidth;
                        n.Height = fheight;
                    }
                }
            }

            c = InputNodes.Count;

            for(int i = 0; i < c; i++)
            {
                Node n;
                if (NodeLookup.TryGetValue(InputNodes[i], out n))
                {
                    InputNode inp = (InputNode)n;
                    inp.TryAndProcess();
                }
            }
        }

        public virtual string GetJson()
        {
            GraphData d = new GraphData();

            List<string> data = new List<string>();

            foreach(Node n in Nodes)
            {
                data.Add(n.GetJson());
            }
            d.name = Name;
            d.nodes = data;
            d.outputs = OutputNodes;
            d.inputs = InputNodes;
            d.defaultTextureType = defaultTextureType;
            d.shiftX = ShiftX;
            d.shiftY = ShiftY;
            d.zoom = Zoom;
            d.hdriIndex = hdriIndex;
            d.parameters = GetJsonReadyParameters();
            d.width = width;
            d.height = height;
            d.customParameters = GetJsonReadyCustomParameters();
            d.customFunctions = GetJsonReadyCustomFunctions();

            return JsonConvert.SerializeObject(d);
        }

        public virtual List<string> GetJsonReadyCustomFunctions()
        {
            List<string> funcs = new List<string>();

            foreach(var f in CustomFunctions)
            {
                funcs.Add(f.GetJson());
            }

            return funcs;
        }

        public virtual Dictionary<string, string> GetJsonReadyParameters()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            foreach (var k in Parameters.Keys)
            {
                parameters[k] = Parameters[k].GetJson();
            }

            return parameters;
        }


        public List<string> GetJsonReadyCustomParameters()
        {
            List<string> parameters = new List<string>();
            foreach (var g in CustomParameters)
            {
                parameters.Add(g.GetJson());
            }
            return parameters;
        }

        public virtual void SetJsonReadyCustomFunctions(List<string> functions)
        {
            if(functions != null)
            {
                CustomFunctions = new List<FunctionGraph>();

                foreach(string k in functions)
                {
                    FunctionGraph g = new FunctionGraph("temp");
                    CustomFunctions.Add(g);
                    g.FromJson(k);
                }

                foreach(FunctionGraph g in CustomFunctions)
                {
                    //set parent graph
                    g.ParentGraph = this;
                    //finally set connections
                    g.SetConnections();
                }
            }
        }

        public virtual void SetJsonReadyParameters(Dictionary<string, string> parameters)
        {
            if (parameters != null)
            {
                Parameters = new Dictionary<string, GraphParameterValue>();

                foreach (var k in parameters.Keys)
                {
                    string[] split = k.Split('.');

                    Node n = null;
                    NodeLookup.TryGetValue(split[0], out n);

                    Parameters[k] = GraphParameterValue.FromJson(parameters[k], n);
                    if(Parameters[k].IsFunction())
                    {
                        var f = Parameters[k].Value as FunctionGraph;
                        f.OnGraphUpdated += Graph_OnGraphUpdated;
                    }
                }
            }
        }

        public virtual void SetJsonReadyCustomParameters(List<string> parameters)
        {
            if(parameters != null)
            {
                CustomParameters.Clear();

                foreach(var k in parameters)
                {
                    CustomParameters.Add(GraphParameterValue.FromJson(k, null));
                }
            }
        }

        public virtual bool Add(Node n)
        {
            if (NodeLookup.ContainsKey(n.Id)) return false;

            if(n is OutputNode)
            {
                OutputNodes.Add(n.Id);
            }
            else if(n is InputNode)
            {
                InputNodes.Add(n.Id);
            }

            NodeLookup[n.Id] = n;
            Nodes.Add(n);

            n.OnUpdate += N_OnUpdate;

            return true;
        }

        private void N_OnUpdate(Node n)
        {
            Updated();
        }

        /// <summary>
        /// This is used in GraphInstanceNodes
        /// We only save the final buffers connected to the outputs,
        /// and release all other buffers to save video card memory
        /// since it is all in video memory and shader based
        /// we do not have to transfer data to the video card
        /// so it will be relatively fast still to update
        /// when we have to recreate the textures
        /// </summary>
        public virtual void ReleaseIntermediateBuffers()
        {
            foreach(Node n in Nodes)
            {
                if(OutputNodes.Contains(n.Id))
                {
                    continue;
                }

                if(n.Buffer != null)
                {
                    n.Buffer.Release();
                }
            }
        }

        public virtual void Remove(Node n)
        {
            if(n is OutputNode)
            {
                OutputNodes.Remove(n.Id);
            }
            else if(n is InputNode)
            {
                InputNodes.Remove(n.Id);
            }

            NodeLookup.Remove(n.Id);
            if(Nodes.Remove(n))
            {
                n.OnUpdate -= N_OnUpdate;
            }
            n.Dispose();
        }

        public virtual Node CreateNode(string type)
        {
            if(ReadOnly)
            {
                return null;
            }

            if(type.Contains(System.IO.Path.PathSeparator))
            {
                var n = new GraphInstanceNode(width, height);
                n.ParentGraph = this;
                return n;
            }

            try
            {
                Type t = Type.GetType(type);
                if(t != null)
                {
                    if (t.Equals(typeof(OutputNode)))
                    {
                        var n  = new OutputNode(defaultTextureType);
                        n.ParentGraph = this;
                        return n;
                    }
                    else if(t.Equals(typeof(InputNode)))
                    {
                        var n = new InputNode(defaultTextureType);
                        n.ParentGraph = this;
                        return n;
                    }
                    else
                    {
                        Node n = (Node)Activator.CreateInstance(t, width, height, defaultTextureType);
                        n.ParentGraph = this;
                        return n;
                    }
                }
                else
                {
                    var n = new GraphInstanceNode(width, height);
                    n.ParentGraph = this;
                    return n;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                return null;
            }
        }

        public virtual void FromJson(string data)
        {
            GraphData d = JsonConvert.DeserializeObject<GraphData>(data);

            if (d != null)
            {
                tempData = new Dictionary<string, Node.NodeData>();
                Dictionary<string, Node> lookup = new Dictionary<string, Node>();
                Dictionary<string, string> nodeData = new Dictionary<string, string>();

                hdriIndex = d.hdriIndex;
                Name = d.name;
                OutputNodes = d.outputs;
                InputNodes = d.inputs;
                defaultTextureType = d.defaultTextureType;
                ShiftX = d.shiftX;
                ShiftY = d.shiftY;
                Zoom = d.zoom;
                width = d.width;
                height = d.height;

                if (width == 0 || width == int.MaxValue) width = 256;
                if (height == 0 || height == int.MaxValue) height = 256;

                SetJsonReadyCustomParameters(d.customParameters);
                SetJsonReadyCustomFunctions(d.customFunctions);

                //parse node data
                //setup initial object instances
                foreach (string s in d.nodes)
                {
                    Node.NodeData nd = JsonConvert.DeserializeObject<Node.NodeData>(s);

                    if (nd != null)
                    {
                        string type = nd.type;
                        if (!string.IsNullOrEmpty(type))
                        {
                            try
                            {
                                Type t = Type.GetType(type);
                                if (t != null)
                                {
                                    //special case to handle output nodes
                                    if (t.Equals(typeof(OutputNode)))
                                    {
                                        OutputNode n = new OutputNode(defaultTextureType);
                                        n.ParentGraph = this;
                                        n.Id = nd.id;
                                        lookup[nd.id] = n;
                                        Nodes.Add(n);
                                        nodeData[nd.id] = s;
                                        tempData[nd.id] = nd;
                                    }
                                    else if(t.Equals(typeof(InputNode)))
                                    {
                                        InputNode n = new InputNode(defaultTextureType);
                                        n.ParentGraph = this;
                                        n.Id = nd.id;
                                        lookup[nd.id] = n;
                                        Nodes.Add(n);
                                        nodeData[nd.id] = s;
                                        tempData[nd.id] = nd;
                                    }
                                    else
                                    {
                                        Node n = (Node)Activator.CreateInstance(t, nd.width, nd.height, defaultTextureType);
                                        if (n != null)
                                        {
                                            n.ParentGraph = this;
                                            n.Id = nd.id;
                                            lookup[nd.id] = n;
                                            Nodes.Add(n);
                                            nodeData[nd.id] = s;
                                            tempData[nd.id] = nd;
                                        }
                                    }
                                }
                                else
                                {
                                    //log we could not load graph node
                                }
                            }
                            catch
                            {
                                //log we could not load graph node
                            }
                        }
                    }
                }

                //after setting initial lookup
                NodeLookup = lookup;

                //and before applying real data to each node...
                //we need to populate the parameters for them
                SetJsonReadyParameters(d.parameters);

                //apply data to nodes
                foreach (Node n in Nodes)
                {
                    string ndata = null;
                    nodeData.TryGetValue(n.Id, out ndata);

                    if(!string.IsNullOrEmpty(ndata))
                    {
                        n.FromJson(lookup, ndata);

                        //origin sizes are only for graph instances
                        //not actually used in the current one being edited
                        //it is used in the ResizeWith
                        OriginSizes[n.Id] = new Point(n.Width, n.Height);
                    }
                }

                if (!(this is FunctionGraph))
                {
                    SetConnections();
                }
            }
        }

        public void SetConnections()
        {
            //finally after every node is populated
            //try and connect them all!
            foreach (Node n in Nodes)
            {
                Node.NodeData nd = null;
                if (tempData.TryGetValue(n.Id, out nd))
                {
                    n.SetConnections(NodeLookup, nd.outputs);
                }
            }

            //then add event handlers!
            foreach(Node n in Nodes)
            {
                n.OnUpdate += N_OnUpdate;
            }

            //release temp data
            tempData.Clear();

            if (this is FunctionGraph)
            {
                (this as FunctionGraph).UpdateOutputTypes();
            }
            else
            {
                TryAndProcess();
            }
        }

        public void CopyResources(string cwd)
        {
            foreach (Node n in Nodes)
            {
                n.CopyResources(cwd);
            }

            //set last in case we need to copy from current graph cwd to new cwd
            this.CWD = cwd;
        }

        public virtual void Dispose()
        {
            if (Nodes != null)
            {
                foreach (Node n in Nodes)
                {
                    n.Dispose();
                }

                Nodes.Clear();
                Nodes = null;
            }
        }

        protected void Updated()
        {
            if(OnGraphUpdated != null)
            {
                OnGraphUpdated.Invoke(this);
            }
        }

        public bool IsParameterValueFunction(string id, string parameter)
        {
            string cid = id + "." + parameter;

            GraphParameterValue v = null;

            if (Parameters.TryGetValue(cid, out v))
            {
                return v.IsFunction();
            }

            return false;
        }

        public bool HasParameterValue(string id, string parameter)
        {
            string cid = id + "." + parameter;

            return Parameters.ContainsKey(cid);
        }

        public GraphParameterValue GetParameterRaw(string id, string parameter)
        {
            GraphParameterValue p = null;

            string cid = id + "." + parameter;

            Parameters.TryGetValue(cid, out p);

            return p;
        }

        public object GetParameterValue(string id, string parameter)
        {
            string cid = id + "." + parameter;

            GraphParameterValue p = null;

            if (Parameters.TryGetValue(cid, out p))
            {
                if (p.IsFunction())
                {
                    FunctionGraph g = p.Value as FunctionGraph;

                    g.TryAndProcess();

                    return g.Result;
                }
                else
                {
                    return p.Value;
                }
            }

            return null;
        } 

        public T GetParameterValue<T>(string id, string parameter)
        {
            string cid = id + "." + parameter;

            GraphParameterValue p = null;

            if(Parameters.TryGetValue(cid, out p))
            {
                if(p.IsFunction())
                {
                    FunctionGraph g = p.Value as FunctionGraph;

                    g.TryAndProcess();

                    if(g.Result == null)
                    {
                        return default(T);
                    }

                    return (T)g.Result;
                }
                else
                {
                    return (T)p.Value;
                }
            }

            return default(T);
        }

        public void RemoveParameterValue(string id, string parameter)
        {
            string cid = id + "." + parameter;

            GraphParameterValue p = null;

            if (Parameters.TryGetValue(cid, out p))
            {
                if (p.IsFunction())
                {
                    FunctionGraph g = p.Value as FunctionGraph;
                    g.OnGraphUpdated -= Graph_OnGraphUpdated;
                    g.Dispose();
                }
            }

            Parameters.Remove(cid);

            Updated();
        }

        public void SetParameterValue(string id, string parameter, object v)
        {
            string cid = id + "." + parameter;

            GraphParameterValue p = null;

            if (Parameters.TryGetValue(cid, out p))
            {
                if (p.IsFunction())
                {
                    FunctionGraph g = p.Value as FunctionGraph;
                    g.OnGraphUpdated -= Graph_OnGraphUpdated;
                    g.Dispose();
                }

                if (v is float || v is int || v is double)
                {
                    p.Type = NodeType.Float;
                }
                else if (v is bool)
                {
                    p.Type = NodeType.Bool;
                }
                else if (v is MVector)
                {
                    p.Type = NodeType.Float4;
                }

                p.Value = v;
            }
            else
            {
                p = Parameters[cid] = new GraphParameterValue(parameter, v);

                if (v is float || v is int || v is double)
                {
                    p.Type = NodeType.Float;
                }
                else if (v is bool)
                {
                    p.Type = NodeType.Bool;
                }
                else if (v is MVector)
                {
                    p.Type = NodeType.Float4;
                }
            }

            if (v is FunctionGraph)
            {
                (v as FunctionGraph).OnGraphUpdated += Graph_OnGraphUpdated;
            }

            Updated();
        }

        private void Graph_OnGraphUpdated(Graph g)
        {
            if(g is FunctionGraph)
            {
                FunctionGraph fg = g as FunctionGraph;

                if (fg.ParentNode != null)
                {
                    fg.ParentNode.TryAndProcess();
                }
            }
        }
    }
}
