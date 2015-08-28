////////////////////////////////////////////////////////////////////////////////////////////////////
//
// CenterCLR.CustomLINQProviderDemo
// Copyright (c) Kouji Matsui, All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification,
// are permitted provided that the following conditions are met:
//
// * Redistributions of source code must retain the above copyright notice,
//   this list of conditions and the following disclaimer.
// * Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO,
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
// IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
// INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
// HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
// EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace CenterCLR.CustomLINQProviderDemo
{
	public sealed class TableSqlGenerator<TModel>
	{
		public TableSqlGenerator(string tableName)
		{
			TableName = tableName;
		}

		public string TableName { get; private set; }

		public WhereSqlGenerator<TModel> Where(Expression<Func<TModel, bool>> filterExpression)
		{
			return new WhereSqlGenerator<TModel>(this, filterExpression);
		}

		public override string ToString()
		{
			return string.Format("SELECT * FROM [{0}]", TableName);
		}
	}

	public sealed class WhereSqlGenerator<TModel>
	{
		private readonly Expression<Func<TModel, bool>> filterExpression_;
		private readonly TableSqlGenerator<TModel> table_;

		public WhereSqlGenerator(
			TableSqlGenerator<TModel> table,
			Expression<Func<TModel, bool>> filterExpression)
		{
			table_ = table;
			filterExpression_ = filterExpression;
		}

		public SelectSqlGenerator<TResultModel, TModel> Select<TResultModel>(
			Expression<Func<TModel, TResultModel>> selectorExpression)
		{
			return new SelectSqlGenerator<TResultModel, TModel>(
				table_,
				filterExpression_,
				selectorExpression);
		}

		public override string ToString()
		{
			return string.Format(
				"SELECT * FROM {0} WHERE {1}",
				table_.TableName,
				filterExpression_.Body);
		}
	}

	public sealed class SelectSqlGenerator<TResultModel, TModel> : IEnumerable<TResultModel>
	{
		private readonly Expression<Func<TModel, bool>> filterExpression_;
		private readonly Expression<Func<TModel, TResultModel>> selectorExpression_;
		private readonly TableSqlGenerator<TModel> table_;

		public SelectSqlGenerator(
			TableSqlGenerator<TModel> table,
			Expression<Func<TModel, bool>> filterExpression,
			Expression<Func<TModel, TResultModel>> selectorExpression)
		{
			table_ = table;
			filterExpression_ = filterExpression;
			selectorExpression_ = selectorExpression;
		}

		private string GetSelectClauseString()
		{
			var newExpression = (NewExpression)selectorExpression_.Body;
			return string.Join(
				",",
				newExpression.Arguments.Select(
					argumentExpression => ((MemberExpression)argumentExpression).Member.Name));
		}

		public IEnumerator<TResultModel> GetEnumerator()
		{
			using (var httpClient = new HttpClient())
			{
				// This is very wrong demonstration query :)
				var url = new Uri("http://api.example.com/v1?rest=" + this.ToString());

				using (var stream = httpClient.GetStreamAsync(url).Result)
				{
					var tr = new StreamReader(stream, Encoding.UTF8);
					var jtr = new JsonTextReader(tr);
					var js = new JsonSerializer();

					IEnumerable<TResultModel> results = js.Deserialize<TResultModel[]>(jtr);

					return results.GetEnumerator();
				}
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public override string ToString()
		{
			return string.Format(
				"SELECT {0} FROM [{1}] WHERE {2}",
				GetSelectClauseString(),
				table_.TableName,
				filterExpression_.Body);
		}
	}

	public sealed class OreOreModel
	{
		public int ID;
		public string Name;
	}

	internal class Program
	{
		private static void Main(string[] args)
		{
			var table = new TableSqlGenerator<OreOreModel>("OreOre");
			var query =
				from oreore in table
				where oreore.ID == 123
				select new { oreore.ID, oreore.Name };

			Console.WriteLine(query);

			foreach (var value in query)
			{
				Console.WriteLine(string.Format("{0}: {1}", value.ID, value.Name));
			}
		}
	}
}
