﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using Lucene.Net.Store;
using Lucene.Net.Index;
using Lucene.Net.Documents;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.Snowball;

namespace SearchEngine
{
    public class IndexingClass
    {
        IndexWriter writer;
        System.IO.StreamReader reader;
        ISet<string> stopwords;      
        public static Analyzer analyzer;
        public static Directory luceneIndexDirectory;
        public static string FieldDOC_ID = "DocID";
        public static string FieldTITLE = "Title";
        public static string FieldAUTHOR = "Author";
        public static string FieldBIBLIO_INFO = "BiblioInfo";
        public static string FieldABSTRACT = "Abstract";
        const Lucene.Net.Util.Version VERSION = Lucene.Net.Util.Version.LUCENE_30;

        public IndexingClass()
        {

        }

        // Create stopwords list
        public ISet<string> Stopwords()
        {
            char[] delimiters = { '\t', ' ', '\r', '\n' };
            ISet<string> stopwordSet = new HashSet<string>(Resource.StopwordList.Split(delimiters, StringSplitOptions.RemoveEmptyEntries).ToList());
            return stopwordSet;
        }

        // Open index and initialize analyzer and indexWriter
        public void OpenIndex(string DirectoryPath, bool stemmingState)
        {
            stopwords = Stopwords();

            // Initilize the class instance
            luceneIndexDirectory = FSDirectory.Open(DirectoryPath);
            IndexWriter.MaxFieldLength mfl = new IndexWriter.MaxFieldLength(IndexWriter.DEFAULT_MAX_FIELD_LENGTH);
            
            // Decide which analyzer should be used
            if (!stemmingState)
                analyzer = new StandardAnalyzer(VERSION, stopwords);               
            else
                analyzer = new SnowballAnalyzer(VERSION, "English", stopwords);

            // Set similarity     
            writer = new IndexWriter(luceneIndexDirectory, analyzer, true, mfl);
            writer.SetSimilarity(new NewSimilarity());
        }

        // Read through all files
        public void WalkDirectoryTree(string collectionPath)
        {
            System.IO.DirectoryInfo root = new System.IO.DirectoryInfo(collectionPath);
            System.IO.FileInfo[] files = null;
            System.IO.DirectoryInfo[] subDirs = null;

            // First, process all the files directly under this folder 
            try
            {
                files = root.GetFiles("*.*");
            }
            catch (UnauthorizedAccessException e)
            {
                MessageBox.Show(e.Message);
            }

            catch (System.IO.DirectoryNotFoundException e)
            {
                MessageBox.Show(e.Message);
            }
            if (files != null)
            {
                // Process every file
                foreach (System.IO.FileInfo fi in files)
                {
                    string name = fi.FullName;
                    IndexingDocuments(name);
                }

                // Now find all the subdirectories under this directory.
                subDirs = root.GetDirectories();

                // Resursive call for each subdirectory.
                foreach (System.IO.DirectoryInfo dirInfo in subDirs)
                {
                    string name = dirInfo.FullName;
                    WalkDirectoryTree(name);
                }
            }     
        }

        // Preprocess documents and add to index
        public void IndexingDocuments(string name)
        {
            string text = null;
            
            // Read the file
            using (reader = new System.IO.StreamReader(name))
            {
                text = reader.ReadToEnd();
            }

            // Preprocessing document (remove abstract error)
            string[] delimiters = { ".I ", ".T", ".A", ".B", ".W" };
            string[] docInfo = text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
            docInfo[4] = docInfo[4].Remove(0, docInfo[1].Length);

            // Creating Index for four different fields
            Field doc_ID = new Field(FieldDOC_ID, docInfo[0], Field.Store.YES, Field.Index.NO, Field.TermVector.NO);
            Field title = new Field(FieldTITLE, docInfo[1], Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Field author = new Field(FieldAUTHOR, docInfo[2], Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Field bibliography = new Field(FieldBIBLIO_INFO, docInfo[3], Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);
            Field abstrat = new Field(FieldABSTRACT, docInfo[4], Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS);

            // Default boosting value = 1, boosting level changes when user specify the value 
            title.Boost = MainSearchForm.titleBoost;
            author.Boost = MainSearchForm.authorBoost;
            bibliography.Boost = MainSearchForm.bibliBoost;
            abstrat.Boost = MainSearchForm.abstractBoost;
            
            // Add to the document
            Document doc = new Document();
            doc.Add(doc_ID);
            doc.Add(title);
            doc.Add(author);
            doc.Add(bibliography);
            doc.Add(abstrat);
            writer.AddDocument(doc);
        }

        // Clean up Indexer
        public void CleanUpIndexer()
        {
            writer.Optimize();
            writer.Flush(true, true, true);
            writer.Dispose();
        }
    }
}
