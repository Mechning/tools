﻿//
// Very Simple Graph for serializing to a DGML file format
//

using System.Collections.Generic;
using System.Xml;
using System.IO;

namespace Walkabout.Utilities
{
    class SimpleGraph
    {
        public Dictionary<string, SimpleGraphNode> Nodes;
        public List<SimpleGraphLink> Links;

        public SimpleGraph()
        {
            Nodes = new Dictionary<string, SimpleGraphNode>();
            Links = new List<SimpleGraphLink>();
        }

        public SimpleGraphNode AddOrGetNode(string Id)
        {
            SimpleGraphNode node;

            if (Nodes.TryGetValue(Id, out node) == false)
            {
                node = new SimpleGraphNode(Id);
                Nodes.Add(Id, node);
            }

            return node;
        }

        public SimpleGraphLink GetOrAddLink(string source, string target)
        {
            SimpleGraphNode nodeSource = AddOrGetNode(source);
            SimpleGraphNode nodeTarget = AddOrGetNode(target);

            SimpleGraphLink link = new SimpleGraphLink(nodeSource, nodeTarget);

            int index = nodeSource.LinkTarget.IndexOf(nodeTarget);

            if (index == -1)
            {
                nodeSource.LinkTarget.Add(nodeTarget);

                // Also update the global Links on the Graph
                Links.Add(link);
            }
            else
            {
                // This link already exist
                foreach (SimpleGraphLink l in Links)
                {
                    if (l.Source == nodeSource && l.Target == nodeTarget)
                    {
                        return l;
                    }
                }
            }


            return link;
        }

        /// <summary>
        /// Save in the DGML format
        /// </summary>
        /// <param name="file"></param>
        public void Save(string file)
        {
            XmlDocument doc = ToXml();
            doc.Save(file);
        }

        /// <summary>
        /// Save in the DGML format
        /// </summary>
        /// <param name="File"></param>
        public void Save(TextWriter writer)
        {
            XmlDocument doc = ToXml();
            doc.Save(writer);
        }

        public XmlDocument ToXml()
        {
            XmlDocument doc = new XmlDocument();
            doc.InnerXml = "<DirectedGraph xmlns=\"http://schemas.microsoft.com/vs/2009/dgml\">" +
                            "</DirectedGraph>";

            string ns = "http://schemas.microsoft.com/vs/2009/dgml";

            XmlNode rootNodes = doc.CreateElement("Nodes", ns);
            doc.DocumentElement.AppendChild(rootNodes);


            foreach (SimpleGraphNode sgn in Nodes.Values)
            {
                XmlElement n = doc.CreateElement("Node", ns);
                n.SetAttribute("Id", sgn.Id);
                if (!string.IsNullOrEmpty(sgn.Category))
                {
                    n.SetAttribute("Category", sgn.Category);
                }
                foreach (SimpleGraphProperty sgp in sgn.Properties)
                {
                    n.SetAttribute(sgp.Id, sgp.Value.ToString());
                }
                rootNodes.AppendChild(n);
            }


            XmlNode rootLinks = doc.CreateElement("Links", ns);
            doc.DocumentElement.AppendChild(rootLinks);

            foreach (SimpleGraphLink sgl in Links)
            {
                XmlElement l = doc.CreateElement("Link", ns);
                l.SetAttribute("Source", sgl.Source.Id);
                l.SetAttribute("Target", sgl.Target.Id);
                if (!string.IsNullOrEmpty(sgl.Category))
                {
                    l.SetAttribute("Category", sgl.Category);
                }

                foreach (SimpleGraphProperty sgp in sgl.Properties)
                {
                    l.SetAttribute(sgp.Id, sgp.Value.ToString());
                }
                rootLinks.AppendChild(l);
            }

            return doc;
        }
    }


    class SimpleGraphNode : SimpleGraphEntry
    {
        public string Id;
        public List<SimpleGraphNode> LinkTarget;

        public SimpleGraphNode(string Id)
        {
            this.Id = Id;
            LinkTarget = new List<SimpleGraphNode>();
        }
    }

    class SimpleGraphLink : SimpleGraphEntry
    {
        public SimpleGraphNode Source;
        public SimpleGraphNode Target;

        public SimpleGraphLink(SimpleGraphNode source, SimpleGraphNode target)
        {
            Source = source;
            Target = target;
        }
    }

    class SimpleGraphEntry
    {
        public List<SimpleGraphProperty> Properties = new List<SimpleGraphProperty>();
        public string Category { get; set; }

        public SimpleGraphProperty AddProperty(string id, object value)
        {
            SimpleGraphProperty sgp = new SimpleGraphProperty();
            sgp.Id = id;
            sgp.Value = value;
            Properties.Add(sgp);
            return sgp;
        }

        public SimpleGraphProperty GetProperty(string id)
        {
            foreach (SimpleGraphProperty sgp in Properties)
            {
                if (sgp.Id == id)
                {
                    return sgp;
                }
            }
            return null;
        }

    }


    class SimpleGraphProperty
    {
        public string Id;
        public object Value;
    }
}
