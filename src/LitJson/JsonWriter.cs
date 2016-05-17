//#define UNITY3D
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace LitJson
{
	internal enum Condition
	{
		InArray,
		InObject,
		NotAProperty,
		Property,
		Value
	}

	internal class WriterContext
	{
		public int Count;
		public bool InArray;
		public bool InObject;
		public bool ExpectingValue;
		public int Padding;
	}

	/// <summary>
	/// Stream-like facility to output JSON text.
	/// </summary>
	public class JsonWriter
	{
		#region Fields

		private static NumberFormatInfo number_format;

		private WriterContext context;
		private Stack<WriterContext> ctx_stack;
		private bool has_reached_end;
		private char[] hex_seq;
		private int indentation;
		private int indent_value;
		private string indent_string;
		private StringBuilder inst_string_builder;
		private bool pretty_print;
		private bool validate;
		private TextWriter writer;

		#endregion


		#region Properties

		public int IndentValue {
			get { return indent_value; }
			set {
				indentation = (indentation / indent_value) * value;
				indent_value = value;
			}
		}
		
		public string IndentString {
			get { return indent_string; }
			set { indent_string = value; }
		}

		public bool PrettyPrint {
			get { return pretty_print; }
			set { pretty_print = value; }
		}

		public TextWriter TextWriter {
			get { return writer; }
		}

		public bool Validate {
			get { return validate; }
			set { validate = value; }
		}

		#endregion


		#region Constructors

		static JsonWriter ()
		{
			number_format = NumberFormatInfo.InvariantInfo;
		}

		public JsonWriter ()
		{
			inst_string_builder = new StringBuilder ();
			writer = new StringWriter (inst_string_builder);

			Init ();
		}

		public JsonWriter (StringBuilder sb) :
			this (new StringWriter (sb))
		{
		}

		public JsonWriter (TextWriter writer)
		{
			if (writer == null)
				throw new ArgumentNullException ("writer");

			this.writer = writer;

			Init ();
		}

		#endregion


		#region Private Methods

		private void DoValidation (Condition cond)
		{
			if (!context.ExpectingValue)
				context.Count++;

			if (!validate)
				return;

			if (has_reached_end)
				throw new JsonException (
					"A complete JSON symbol has already been written");

			switch (cond) {
			case Condition.InArray:
				if (!context.InArray)
					throw new JsonException (
						"Can't close an array here");
				break;

			case Condition.InObject:
				if (!context.InObject || context.ExpectingValue)
					throw new JsonException (
						"Can't close an object here");
				break;

			case Condition.NotAProperty:
				if (context.InObject && !context.ExpectingValue)
					throw new JsonException (
						"Expected a property");
				break;

			case Condition.Property:
				if (!context.InObject || context.ExpectingValue)
					throw new JsonException (
						"Can't add a property here");
				break;

			case Condition.Value:
				if (!context.InArray &&
				    (!context.InObject || !context.ExpectingValue))
					throw new JsonException (
						"Can't add a value here");

				break;
			}
		}

		private void Init ()
		{
			has_reached_end = false;
			hex_seq = new char[4];
			indentation = 0;
			indent_value = 4;
			indent_string = " ";
			pretty_print = false;
			validate = true;

			ctx_stack = new Stack<WriterContext> ();
			context = new WriterContext ();
			ctx_stack.Push (context);
		}

		private static void IntToHex (int n, char[] hex)
		{
			int num;

			for (int i = 0; i < 4; i++) {
				num = n % 16;

				if (num < 10)
					hex [3 - i] = (char)('0' + num);
				else
					hex [3 - i] = (char)('A' + (num - 10));

				n >>= 4;
			}
		}

		private void Indent ()
		{
			if (pretty_print)
				indentation += indent_value;
		}


		private void Put (string str)
		{
			if (pretty_print && !context.ExpectingValue)
				for (int i = 0; i < indentation; i++)
					writer.Write (indent_string);

			writer.Write (str);
		}

		private void PutNewline ()
		{
			PutNewline (true);
		}

		private void PutNewline (bool add_comma)
		{
			if (add_comma && !context.ExpectingValue &&
			    context.Count > 1)
				writer.Write (',');

			if (pretty_print && !context.ExpectingValue)
				writer.Write ('\n');
		}

		private void PutString (string str)
		{
			Put (String.Empty);

			writer.Write ('"');

			int n = str.Length;
			for (int i = 0; i < n; i++) {
				switch (str [i]) {
				case '\n':
					writer.Write ("\\n");
					continue;

				case '\r':
					writer.Write ("\\r");
					continue;

				case '\t':
					writer.Write ("\\t");
					continue;

				case '"':
				case '\\':
					writer.Write ('\\');
					writer.Write (str [i]);
					continue;

				case '\f':
					writer.Write ("\\f");
					continue;

				case '\b':
					writer.Write ("\\b");
					continue;
				}

				if ((int)str [i] >= 32 && (int)str [i] <= 126) {
					writer.Write (str [i]);
					continue;
				}

				// Default, turn into a \uXXXX sequence
				IntToHex ((int)str [i], hex_seq);
				writer.Write ("\\u");
				writer.Write (hex_seq);
			}

			writer.Write ('"');
		}

		private void Unindent ()
		{
			if (pretty_print)
				indentation -= indent_value;
		}

		#endregion


		public override string ToString ()
		{
			if (inst_string_builder == null)
				return String.Empty;

			return inst_string_builder.ToString ();
		}

		public void Reset ()
		{
			has_reached_end = false;

			ctx_stack.Clear ();
			context = new WriterContext ();
			ctx_stack.Push (context);

			if (inst_string_builder != null)
				inst_string_builder.Remove (0, inst_string_builder.Length);
		}

		public void Write (bool boolean)
		{
			DoValidation (Condition.Value);
			PutNewline ();

			Put (boolean ? "true" : "false");

			context.ExpectingValue = false;
		}

		public void Write (decimal number)
		{
			DoValidation (Condition.Value);
			PutNewline ();

			Put (Convert.ToString (number, number_format));

			context.ExpectingValue = false;
		}

		public void Write (double number)
		{
			DoValidation (Condition.Value);
			PutNewline ();

			string str = Convert.ToString (number, number_format);
			Put (str);

			if (str.IndexOf ('.') == -1 &&
			    str.IndexOf ('E') == -1)
				writer.Write (".0");

			context.ExpectingValue = false;
		}

		public void Write (int number)
		{
			DoValidation (Condition.Value);
			PutNewline ();

			Put (Convert.ToString (number, number_format));

			context.ExpectingValue = false;
		}

		public void Write (long number)
		{
			DoValidation (Condition.Value);
			PutNewline ();

			Put (Convert.ToString (number, number_format));

			context.ExpectingValue = false;
		}

		public void Write (string str)
		{
			DoValidation (Condition.Value);
			PutNewline ();

			if (str == null)
				Put ("null");
			else
				PutString (str);

			context.ExpectingValue = false;
		}

		[CLSCompliant (false)]
		public void Write (ulong number)
		{
			DoValidation (Condition.Value);
			PutNewline ();

			Put (Convert.ToString (number, number_format));

			context.ExpectingValue = false;
		}

		public void WriteArrayEnd ()
		{
			DoValidation (Condition.InArray);
			PutNewline (false);

			ctx_stack.Pop ();
			if (ctx_stack.Count == 1)
				has_reached_end = true;
			else {
				context = ctx_stack.Peek ();
				context.ExpectingValue = false;
			}

			Unindent ();
			Put ("]");
		}

		public void WriteArrayStart ()
		{
			DoValidation (Condition.NotAProperty);
			PutNewline ();

			Put ("[");

			context = new WriterContext ();
			context.InArray = true;
			ctx_stack.Push (context);

			Indent ();
		}

		public void WriteObjectEnd ()
		{
			DoValidation (Condition.InObject);
			PutNewline (false);

			ctx_stack.Pop ();
			if (ctx_stack.Count == 1)
				has_reached_end = true;
			else {
				context = ctx_stack.Peek ();
				context.ExpectingValue = false;
			}

			Unindent ();
			Put ("}");
		}

		public void WriteObjectStart ()
		{
			DoValidation (Condition.NotAProperty);
			PutNewline ();

			Put ("{");

			context = new WriterContext ();
			context.InObject = true;
			ctx_stack.Push (context);

			Indent ();
		}

		public void WritePropertyName (string property_name)
		{
			DoValidation (Condition.Property);
			PutNewline ();

			PutString (property_name);

			if (pretty_print) {
				if (property_name.Length > context.Padding)
					context.Padding = property_name.Length;

				for (int i = context.Padding - property_name.Length;
                     i >= 0; i--)
					writer.Write (' ');

				writer.Write (": ");
			} else
				writer.Write (':');

			context.ExpectingValue = true;
		}

		#if UNITY3D
		#region Unity specific

		public void Write (UnityEngine.Vector2 v2)
		{
			if (v2 == null)
				return;

			WriteObjectStart ();
			WritePropertyName("x");
			Write(v2.x);
			WritePropertyName("y");
			Write(v2.y);
			WriteObjectEnd ();
		}

		public void Write (UnityEngine.Vector3 v3)
		{
			if (v3 == null)
				return;

			WriteObjectStart ();
			WritePropertyName("x");
			Write(v3.x);
			WritePropertyName("y");
			Write(v3.y);
			WritePropertyName("z");
			Write(v3.z);
			WriteObjectEnd ();
		}

		public void Write (UnityEngine.Vector4 v4)
		{
			if (v4 == null)
				return;

			WriteObjectStart ();
			WritePropertyName("x");
			Write(v4.x);
			WritePropertyName("y");
			Write(v4.y);
			WritePropertyName("z");
			Write(v4.z);
			WritePropertyName("z");
			Write(v4.w);
			WriteObjectEnd ();
		}

		public void Write (UnityEngine.Quaternion q)
		{
			if (q == null)
				return;

			WriteObjectStart ();
			WritePropertyName("x");
			Write(q.x);
			WritePropertyName("y");
			Write(q.y);
			WritePropertyName("z");
			Write(q.z);
			WritePropertyName("z");
			Write(q.w);
			WriteObjectEnd ();
		}

		public void Write (UnityEngine.Matrix4x4 m)
		{
			if (m == null)
				return;

			WriteObjectStart ();
			WritePropertyName("m00");
			Write(m.m00);
			WritePropertyName("m33");
			Write(m.m33);
			WritePropertyName("m23");
			Write(m.m23);
			WritePropertyName("m13");
			Write(m.m13);
			WritePropertyName("m03");
			Write(m.m03);
			WritePropertyName("m32");
			Write(m.m32);
			WritePropertyName("m12");
			Write(m.m12);
			WritePropertyName("m02");
			Write(m.m02);
			WritePropertyName("m22");
			Write(m.m22);
			WritePropertyName("m21");
			Write(m.m21);
			WritePropertyName("m11");
			Write(m.m11);
			WritePropertyName("m01");
			Write(m.m01);
			WritePropertyName("m30");
			Write(m.m30);
			WritePropertyName("m20");
			Write(m.m20);
			WritePropertyName("m10");
			Write(m.m10);
			WritePropertyName("m31");
			Write(m.m31);
			WriteObjectEnd ();
		}

		public void Write (UnityEngine.Ray r)
		{
			WriteObjectStart ();
			WritePropertyName ("origin");
			Write (r.origin);
			WritePropertyName ("direction");
			Write (r.direction);
			WriteObjectEnd ();
		}

		public void Write (UnityEngine.RaycastHit r)
		{
			WriteObjectStart ();
			WritePropertyName ("barycentricCoordinate");
			Write (r.barycentricCoordinate);
			WritePropertyName ("distance");
			Write (r.distance);
			WritePropertyName ("normal");
			Write (r.normal);
			WritePropertyName ("point");
			Write (r.point);
			WriteObjectEnd ();
		}

		public void Write (UnityEngine.Color c)
		{
			if (c == null)
				return;
			
			WriteObjectStart ();
			Put (string.Format ("\"r\":{0},\"g\":{1},\"b\":{2},\"a\":{3}", c.r, c.g, c.b, c.a));
			WriteObjectEnd ();
		}

		#endregion

		#endif
	}
}
