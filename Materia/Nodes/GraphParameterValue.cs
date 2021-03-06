﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Materia.Nodes.Attributes;
using Materia.MathHelpers;
using Newtonsoft.Json;

namespace Materia.Nodes
{
    public class GraphParameterValue
    {
        public delegate void GraphParameterUpdate(GraphParameterValue param);
        public static event GraphParameterUpdate OnGraphParameterUpdate;
        public static event GraphParameterUpdate OnGraphParameterTypeChanged;

        [TextInput]
        public string Name { get; set; }

        protected NodeType type;
        [Dropdown(null)]
        public NodeType Type
        {
            get
            {
                return type;
            }
            set
            {
                type = value;
                if (!(v is float) && type == NodeType.Float)
                {
                    v = 0;
                }
                else if (!(v is bool) && type == NodeType.Bool)
                {
                    v = false;
                }
                else if (!(v is MVector) && (type == NodeType.Float2 || type == NodeType.Float3 || type == NodeType.Float4 || type == NodeType.Gray || type == NodeType.Color))
                {
                    v = new MVector();
                }

                if (OnGraphParameterTypeChanged != null)
                {
                    OnGraphParameterTypeChanged.Invoke(this);
                }
            }
        }

        [TextInput]
        public string Description { get; set; }

        protected float min;
        public float Min
        {
            get
            {
                return min;
            }
            set
            {
                min = value;
                if (OnGraphParameterTypeChanged != null)
                {
                    OnGraphParameterTypeChanged.Invoke(this);
                }
            }
        }

        protected float max;
        public float Max
        {
            get
            {
                return max;
            }
            set
            {
                max = value;
                if (OnGraphParameterTypeChanged != null)
                {
                    OnGraphParameterTypeChanged.Invoke(this);
                }
            }
        }

        protected object v;
        [HideProperty]
        public object Value
        {
            get
            {
                return v;
            }
            set
            {
                v = value;
                if (OnGraphParameterUpdate != null)
                {
                    OnGraphParameterUpdate.Invoke(this);
                }
            }
        }

        [HideProperty]
        public float FloatValue
        {
            get
            {
                float m = Convert.ToSingle(v);
                if (m < Min) m = Min;
                if (m > Max) m = Max;
                return m;
            }
        }

        [HideProperty]
        public int IntValue
        {
            get
            {
                return Convert.ToInt32(v);
            }
        }

        [HideProperty]
        public bool BoolValue
        {
            get
            {
                return Convert.ToBoolean(v);
            }
        }

        [HideProperty]
        public MVector VectorValue
        {
            get
            {
                if (v is MVector)
                {
                    MVector m = (MVector)v;

                    if (m.X < Min) m.X = Min;
                    if (m.Y < Min) m.Y = Min;
                    if (m.Z < Min) m.Z = Min;
                    if (m.W < Min) m.W = Min;

                    if (m.X > Max) m.X = Max;
                    if (m.Y > Max) m.Y = Max;
                    if (m.Z > Max) m.Z = Max;
                    if (m.W > Max) m.W = Max;

                    return m;
                }
                else
                {
                    return new MVector();
                }
            }
        }

        public class GraphParameterValueData
        {
            public string name;
            public object value;
            public bool isFunction;
            public string description;
            public int type;
            public float min;
            public float max;
        }

        public GraphParameterValue(string name, object value,
            string desc = "", NodeType type = NodeType.Float, float min = 0, float max = 1)
        {
            Name = name;
            v = value;
            Description = desc;
            this.type = type;

            if (!(value is float) && !(value is double) && !(value is int) && type == NodeType.Float)
            {
                v = 0;
            }
            else if (!(value is bool) && type == NodeType.Bool)
            {
                v = false;
            }
            else if (!(value is MVector) && (type == NodeType.Float2 || type == NodeType.Float3 || type == NodeType.Float4 || type == NodeType.Gray || type == NodeType.Color))
            {
                //for right now the only possible JObject is a MVector
                if (value is Newtonsoft.Json.Linq.JObject)
                {
                    Newtonsoft.Json.Linq.JObject jp = value as Newtonsoft.Json.Linq.JObject;

                    try
                    {
                        string sjp = jp.ToString();
                        v = JsonConvert.DeserializeObject<MVector>(sjp);
                    }
                    catch
                    {
                        v = null;
                    }

                    if (v == null || v is Newtonsoft.Json.Linq.JObject)
                    {
                        v = new MVector();
                    }
                }
                else
                {
                    v = new MVector();
                }
            }

            this.min = min;
            this.max = max;
        }

        public bool IsFunction()
        {
            if (Value == null) return false;
            return Value is FunctionGraph;
        }

        public string GetJson()
        {
            GraphParameterValueData d = new GraphParameterValueData();
            d.name = Name;
            d.isFunction = IsFunction();
            d.description = Description;
            d.type = (int)Type;
            d.min = Min;
            d.max = Max;

            if (d.isFunction)
            {
                FunctionGraph g = Value as FunctionGraph;

                d.value = g.GetJson();
            }
            else
            {
                d.value = Value;
            }

            return JsonConvert.SerializeObject(d);
        }

        public static GraphParameterValue FromJson(string data, Node n)
        {
            GraphParameterValueData d = JsonConvert.DeserializeObject<GraphParameterValueData>(data);

            if (d.isFunction)
            {
                //we have a chicken and egg problem here...
                //which comes first the node population
                //or the parameter population?
                FunctionGraph t = new FunctionGraph("temp");
                t.FromJson((string)d.value);
                t.ParentNode = n;
                t.SetConnections();
                return new GraphParameterValue(d.name, t, d.description, (NodeType)d.type, d.min, d.max);
            }


            return new GraphParameterValue(d.name, d.value, d.description, (NodeType)d.type, d.min, d.max);
        }
    }
}
