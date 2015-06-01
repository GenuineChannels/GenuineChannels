using System;

namespace Belikov.GenuineChannels.GenuineXHttp
{
	/// <summary>
	/// Parses HTTP headers.
	/// Recognizes the version of the HTTP protocol and Content-length and Expect fields.
	/// </summary>
	internal class GXHTTPHeaderParser
	{
		/// <summary>
		/// Constructs an instance of the HeaderParse class.
		/// </summary>
		/// <param name="client">Specifies the parsing logic. True, to parse an HTTP request; false, otherwise.</param>
		public GXHTTPHeaderParser(bool client)
		{
			this._contentLength = -1;

			this._http11_100continueIsParsed = false;

			this._isHttp11 = false;
			this._firstLineIsParsed = false;

			this._lineNumber = -1;
			this._client = client;
		}

		/// <summary>
		/// Defines types of HTTP header fields.
		/// </summary>
		public enum HeaderFields
		{
			ContentLength,
			Expect100Continue,
			FirstLine,
			OtherField
		}

		private bool _contentLengthIsParsed
		{
			get { return _contentLength != -1; }
		}

		private bool _http11_100continueIsParsed;

		private bool _firstLineIsParsed;

		/// <summary>
		/// An integer that indicates the current line number.
		/// </summary>		
		private int _lineNumber;

		/// <summary>
		/// True, if the object parses an HTTP response; false, otherwise.
		/// </summary>
		private bool _client;

		/// <summary>
		/// Gets the length of the HTTP content. Answers -1 if the field was not parsed.
		/// </summary>
		public long ContentLength
		{
			get { return this._contentLength; }
		}
		private long _contentLength = -1;

		/// <summary>
		/// Gets a boolean value indicating whether 100-continue HTTP mode is requested.
		/// </summary>
		public bool Http11_100Continue
		{
			get { return this._http11_100Continue; }
		}
		private bool _http11_100Continue;

		/// <summary>
		/// Gets a boolean value indicating the protocol version of the HTTP packet.
		/// </summary>
		public bool IsHttp11
		{
			get { return this._isHttp11; }
		}
		private bool _isHttp11;

		/// <summary>
		/// Parses the specified HTTP header line.
		/// </summary>
		/// <param name="line">The line of the HTTP header.</param>
		/// <param name="indexOfFirstDigit">The index of the first digit in the line.</param>
		/// <param name="indexOfLastDigit">The index of the last digit in the line.</param>
		/// <returns>The type of HTTP header field.</returns>
		public HeaderFields ParseHeader(string line, int indexOfFirstDigit, int indexOfLastDigit)
		{
			this._lineNumber++;

			if (!this._firstLineIsParsed)
			{
				int indexOfHttp = line.IndexOf("TTP/");
				if (indexOfHttp < 0)
					throw GenuineExceptions.Get_Receive_IncorrectData();

				this._firstLineIsParsed = true;
				this._isHttp11 = line.Substring(indexOfHttp + 4, 3).CompareTo("1.1") == 0;

				if (line.IndexOf("409", indexOfHttp + 1) >= 0)
					throw GenuineExceptions.Get_Receive_ConflictOfConnections();

				return HeaderFields.FirstLine;
			}

			if (this._client && !_firstLineIsParsed)
				throw GenuineExceptions.Get_Receive_IncorrectData();

			if (!this._contentLengthIsParsed && line.IndexOf("CONTENT-LENGTH")>=0)
			{
				this._contentLength = Convert.ToInt64(line.Substring(indexOfFirstDigit, indexOfLastDigit - indexOfFirstDigit + 1));
				return HeaderFields.ContentLength;
			}

			if (!this._client)
			{
				int indexOfExpect;
				if( !this._http11_100continueIsParsed && (indexOfExpect=line.IndexOf("EXPECT"))>=0 && line.IndexOf("100", indexOfExpect+1)>=0)
				{
					this._http11_100continueIsParsed = true;
					this._http11_100Continue = true;

					return HeaderFields.Expect100Continue;
				}					
			}

			if (this._lineNumber > 30)
				throw GenuineExceptions.Get_Receive_IncorrectData();

			return HeaderFields.OtherField;
		}
	}
}
