// Przy EnableDefaultCompileItems=false SDK-owy GlobalUsings.g.cs nie jest
// dolaczany do kompilacji, a LINKOWANE zrodla Core licza na globalne usingi
// swojego projektu. Podajemy dokladnie ten zestaw jawnie, zamiast dopisywac
// usingi do cudzych, odebranych plikow.
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
