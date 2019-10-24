using Microsoft.Extensions.Configuration;
using QIWI.json;
using QIWI_API;
using Shop.DataAccess;
using Shop.Domain;
using Shop.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/*
		1. Регистрация и вход (смс-код / email код) - сделать до 11.10 (Email есть на метаните)
		2. История покупок 
		3. Категории и товары (картинка в файловой системе) 
		4. Покупка (корзина), оплата и доставка (PayPal/Qiwi/etc)
		5. Комментарии и рейтинги
		6. Поиск (пагинация - постраничность)

		Кто сделает 2 версии (Подключенный и EF) получит автомат на экзамене
*/

namespace Shop.UI
{
	class Program
	{
		static BCryptHasher bCryptHasher = new BCryptHasher();
		static IConfigurationBuilder builder = new ConfigurationBuilder()
							.SetBasePath(Directory.GetCurrentDirectory())
							.AddJsonFile("appsettings.json", false, true);

		static IConfigurationRoot configurationRoot = builder.Build();
		//static string connectionString = configurationRoot.GetConnectionString("HomeConnectionString");
		static string connectionString = configurationRoot.GetConnectionString("ITStepConnectionString");
		static string providerName = configurationRoot
							.GetSection("AppConfig")
							.GetChildren().Single(item => item.Key == "ProviderName")
							.Value;

		static User user = null;

		static void Main(string[] args)
		{
			Entry();

			//Search();
			//Pagination();
			//Test();
			//Registration();
			//SignIn();
		}

		static void Entry()
		{
			int entryAnswer = -1;
			while (entryAnswer != 0)
			{
				Console.Clear();
				Console.WriteLine("1. Регистрация");
				Console.WriteLine("2. Вход");
				Console.WriteLine("0. Выход");
				Console.Write("Выберите действие: ");
				if (Int32.TryParse(Console.ReadLine(), out entryAnswer) == false || entryAnswer < 0)
				{
					entryAnswer = -1;  // При выпадении false в Int32.TryParse параметр out page присваивается значние 0, и с новым циклом программа завершает работу
					WriteMessage("Введенное действие не является корректным. Выберите действие из списка!");
					continue;
				}
				switch (entryAnswer)
				{
					case 1:
						Registration();
						WriteMessage("Ваш аккаунт зарегестрирован!");
						break;
					case 2:
						if (!Authorization())
						{
							WriteMessage("Превышено кол-во попыток авторизации. Переход на главную страницу");
							continue;
						}
						WriteMessage("Вы успешно авторизовались!");
						Menu();
						break;
					case 0:
						entryAnswer = 0;
						break;
				}
			}
		}

		static void FillBalance()
		{
			string token;
			int fillSum;
			Console.WriteLine("Введите ваш QIWI token:");
			token = Console.ReadLine();
			Console.WriteLine("Введите сумму для пополнения:");
			fillSum = Int32.Parse(Console.ReadLine());
			//QIWI_API_Class q_raw = new QIWI_API_Class("af7899285396f3a5ee94f5545d210f5d"); // токен от 04
			//QIWI_API_Class q_raw = new QIWI_API_Class("cff2e4ebb1cb99dd86863e868bbf1eae"); // токен от 03
			QIWI_API_Class q_raw = new QIWI_API_Class(token);
			//QiwiCurrentBalanceClass cur_balance = q_raw.CurrentBalance; // Получить информацию о балансе кошелька
			//Console.WriteLine(cur_balance.GetQiwiBalance);
			QiwiTransactionsUniversalDetailsPaymentClass details_universal_transaction = new QiwiTransactionsUniversalDetailsPaymentClass(); // Этот набор реквизитов универсален. Подходит для перевода на QIWI, для пополнения баланса, для перевода на карту банка и другие переводы, которые требуют один реквизит [номер получателя]
			details_universal_transaction.comment = "Производится пополнение счета"; // комментарий
			details_universal_transaction.sum.amount = fillSum; // сумма
			details_universal_transaction.fields = new QiwiFieldsPaymentUniversalDirectClass() { account = "77078510204" }; // такой формат подойдёт для пеервода на QIWI, на карту банка или для пополнения баланса телефона

			QiwiResultSendUniversalPaymentClass SendUniversalPayment = q_raw.SendPayment("99", details_universal_transaction); // отправить платёж. Получатель для пополнения баланса мобильного телефона без семёрки/восмёрки (формат: 9995554422). Для перевода на киви в с семёркой (формат: 79995554422). Либо номер карты и т.п.
			if (SendUniversalPayment == null)
			{
				Console.WriteLine("Платеж отменен");
				return;
			}
			else
			{
				WriteMessage("Платеж успешно совершен");
				user.Balance += fillSum * 1000;
				UpdateUser();
			}
			Console.ReadLine();
		}

		private static string ConsoleReadLineButHashed()
		{
			ConsoleKeyInfo key;
			StringBuilder sb = new StringBuilder();
			while ((key = Console.ReadKey(true)).Key != ConsoleKey.Enter)
			{
				sb.Append(key.KeyChar);
				Console.Write("*");
			}
			return sb.ToString();
		}

		private static bool Authorization()
		{
			Console.Clear();
			string truePassword = null, trueVerCode;
			string login, password, verCode;
			bool wrong = false;
			int attemptToLogIn = 0;
			Console.WriteLine("Введите Логин: ");
			login = Console.ReadLine();
			if (LoginExist(login))
			{
				truePassword = getHashedPassByLogin(login);
			}
			else
			{
				wrong = true;
			}
			Console.WriteLine("Введите пароль: ");
			password = ConsoleReadLineButHashed();
			Console.WriteLine();
			if (!wrong)
			{
				if (!bCryptHasher.VerifyPassword(truePassword, password))
				{
					wrong = true;
				}
			}
			if (wrong)
			{
				while (wrong && attemptToLogIn < 3)
				{
					Console.WriteLine("Ошибка! Неверный логин или пароль! Попробуйте снова.");
					Console.WriteLine($"Осталось попыток - {3 - attemptToLogIn++}");
					Console.WriteLine("Введите Логин: ");
					login = Console.ReadLine();
					if (LoginExist(login))
					{
						truePassword = getHashedPassByLogin(login);
						wrong = false;
					}
					else
					{
						wrong = true;
					}
					Console.WriteLine("Введите пароль: ");
					password = ConsoleReadLineButHashed();
					Console.WriteLine();
					if (!wrong)
					{
						if (!bCryptHasher.VerifyPassword(truePassword, password))
						{
							wrong = true;
						}
						else
						{
							wrong = false;
						}
					}
				}
			}
			if (wrong)
			{
				return false;
			}
			trueVerCode = getHashedVerCodeByLogin(login);
			Console.WriteLine("Введите секретный код: ");
			verCode = ConsoleReadLineButHashed();
			Console.WriteLine();
			if (!bCryptHasher.VerifyPassword(trueVerCode, verCode))
			{
				wrong = true;
			}
			if (wrong)
			{
				while (wrong && attemptToLogIn < 3)
				{
					Console.WriteLine("Ошибка! Неверный секретный код! Попробуйте снова.");
					Console.WriteLine($"Осталось попыток - {3 - attemptToLogIn++}");
					Console.WriteLine("Введите секретный код: ");
					verCode = ConsoleReadLineButHashed();
					Console.WriteLine();
					if (!bCryptHasher.VerifyPassword(trueVerCode, verCode))
					{
						wrong = true;
					}
					else
					{
						wrong = false;
					}
				}
			}
			if (wrong)
			{
				return false;
			}
			using (var context = new ShopContext(connectionString))
			{
				var result = from user_
								 in context.Users
										 where user_.Login.Equals(login)
										 select user_;
				user = result.First();
			}
			return true;
		}

		private static bool LoginExist(string login)
		{
			using (var context = new ShopContext(connectionString))
			{
				var result = from user_
										 in context.Users
										 where user_.Login.Equals(login)
										 select user_;
				if (result.ToList().Count > 0)
				{
					return true;
				}
				return false;
			}
		}

		private static string getHashedPassByLogin(string login)
		{
			string truePassword;
			using (var context = new ShopContext(connectionString))
			{
				var result = from user_
										 in context.Users
										 where user_.Login.Equals(login)
										 select user_;
				truePassword = result.First().Password;
			}

			return truePassword;
		}

		private static string getHashedVerCodeByLogin(string login)
		{
			string trueVerCode;
			using (var context = new ShopContext(connectionString))
			{
				var result = from user_
										 in context.Users
										 where user_.Login.Equals(login)
										 select user_;
				trueVerCode = result.First().VerificationCode;
			}

			return trueVerCode;
		}

		private static void Menu()
		{
			int menuAnswer = -1;
			while (menuAnswer != 0)
			{
				Console.WriteLine("1. Мой аккаунт");
				Console.WriteLine("2. Поиск товаров");
				Console.WriteLine("3. Все категории и товары");
				Console.WriteLine("0. Выход");
				Console.Write("Выберите действие: ");
				if (Int32.TryParse(Console.ReadLine(), out menuAnswer) == false || menuAnswer < 0)
				{
					menuAnswer = -1;  // При выпадении false в Int32.TryParse параметр out menuAnswer присваивается значние 0, и с новым циклом программа завершает работу
					WriteMessage("Введенное действие не является корректным. Выберите действие из списка!");
					continue;
				}
				switch (menuAnswer)
				{
					case 1:
						int infoAnswer = -1;
						//int comments;
						//using (var context = new ShopContext(connectionString))
						//{
						//	var countComments = context.Users.Join(context.Reviews, // второй набор
						//	user => user.Id, // свойство-селектор объекта из первого набора
						//	comment => comment.UserId, // свойство-селектор объекта из второго набора
						//	(p, c) => new // результат
						//	{
						//		Name = p.FullName
						//	});
						//	comments = countComments.Count();
						//}
						while (infoAnswer != 0)
						{
							Console.Clear();
							Console.WriteLine("Информация о вашем аккаунте:");
							Console.WriteLine($"\tУникальный идентификатор: {user.Id}");
							Console.WriteLine($"\tДата регистрации: {user.CreationDate}");
							Console.WriteLine($"\tЛогин: {user.Login}");
							Console.WriteLine($"\tНа счету: {user.Balance}");
							Console.WriteLine($"\tПолное имя: {user.FullName}");
							Console.WriteLine($"\tНомер телефона: {user.PhoneNumber}");
							Console.WriteLine($"\tПочта: {user.Email}");
							Console.WriteLine($"\tАдрес доставки: {user.Address}");
							Console.WriteLine($"\tПароль: {user.Password}");
							Console.WriteLine($"\tСекретный код: {user.VerificationCode}");
							//Console.WriteLine($"\tВсего комментариев: {comments}");
							Console.WriteLine($"---------------------------------------");
							Console.WriteLine($"1. История покупок");
							Console.WriteLine($"2. Пополнить баланс");
							Console.WriteLine($"0. Назад");
							if (Int32.TryParse(Console.ReadLine(), out infoAnswer) == false || infoAnswer < 0 || infoAnswer > 2)
							{
								infoAnswer = -1;  // При выпадении false в Int32.TryParse параметр out infoAnswer присваивается значние 0, и с новым циклом программа завершает работу
								WriteMessage("Введенное действие не является корректным. Выберите действие из списка!");
								continue;
							}
							switch (infoAnswer)
							{
								case 1:
									Console.Clear();
									List<Purchase> purchases = new List<Purchase>();
									using (var context = new ShopContext(connectionString))
									{
										var result = from purchase
																 in context.Purchases
																 where purchase.UserId.Equals(user.Id)
																 orderby purchase.CreationDate
																 select purchase;
										purchases = result.ToList();
									}
									using (var context = new ShopContext(connectionString))
									{
										int num = 1;
										Console.WriteLine($"  Товар\t\tЦена(тг.)\t(Дата покупки)");
										foreach (var purchase in purchases)
										{
											var result = from item
																	 in context.Items
																	 where item.Id.Equals(purchase.ItemId)
																	 select item;
											Item tempItem = result.First();
											Console.WriteLine($"{num++}){tempItem.Name}\t\t{tempItem.Price}\t{purchase.CreationDate}");
										}
									}
									Console.ReadLine();
									break;
								case 2:
									FillBalance();
									//int refill;
									//Console.WriteLine("Введите сумму для пополнения: ");
									//if (Int32.TryParse(Console.ReadLine(), out refill) == false || refill < 0)
									//{
									//	WriteMessage("Введенная сумма не является корректной. Возврат назад.");
									//	infoAnswer = 0;
									//	continue;
									//}
									//Console.WriteLine("На ваш email высланы инструкции по пополнению. // Но так как это еще не реализовано то он автоматически пополнит баланс счета");
									//user.Balance += refill;
									//UpdateUser();
									//Console.WriteLine("Пополнение завершено!");
									//Console.ReadLine();
									break;
								case 0:
									infoAnswer = 0;
									break;
							}
						}
						Console.Clear();
						break;
					case 2:
						Search();
						break;
					case 3:
						Categories();
						break;
					case 0:
						menuAnswer = 0;
						break;
				}
			}
		}

		private static void UpdateUser()
		{
			using (var context = new ShopContext(connectionString))
			{
				var result = context.Users.Update(user);
				context.SaveChanges();
			}
		}

		static void ProcessCollections()
		{
			List<string> cityNames = new List<string>
			{
				"Almaty", "Ankara", "Boriswill", "Nur-Sultan", "Yalta"
			};

			List<string> processedCityNames = new List<string>(); // для поиска товаров от пользователя
			foreach (string name in cityNames)
			{
				if (name.Contains("-"))
				{
					processedCityNames.Add(name);
				}
			}

			var result = from name
									 in cityNames
									 where name.Contains("-")
									 select name;

			var shortResult = cityNames.Where(name => name.Contains("-"));
			var shortResult2 = cityNames.Select(name => name.Contains("-"));
		}

		private static void Test()
		{
			Category category = new Category
			{
				Name = "Бытовая техника",
				//ImagePath = "C:/data",
			};


			Item item = new Item
			{
				Name = "Пылесос",
				//ImagePath = "C:/data/items",
				//Price = 25999,
				//Description = "Обычный пылесос",
				CategoryId = category.Id
			};

			User user = new User
			{
				Login = "Vanya",
				FullName = "Иван Иванов",
				PhoneNumber = "123456",
				Email = "qwer@qwr.qwr",
				Address = "Twesd, 12",
				Password = "password",
				VerificationCode = "1234"
			};

			User userForComment1 = new User
			{
				Login = "Alex"
			};
			User userForComment2 = new User
			{
				Login = "Sam"
			};

			for (int i = 0; i < 10; i++)
			{

				Review comment1 = new Review
				{
					UserId = userForComment1.Id,
					ItemId = item.Id,
					Rate = 5,
					Value = "Довольно мощный простой пылесос.Легкий, компактный, можно спрятать в небольшую нишу. Нравится, что пыль собирается в отдельную емкость, которую можно вытряхнуть и тут же помыть контейнер. Щетка не забивается шерстью и волосами. При работе пылесос чуть электризуется и корпус в части,где прикрепляется шланг, чуть покрывается тонким слоем пыли. Просто протираю тряпкой после использования. Не очень мобильная щетка, приходится прилагать легкое усилие, чтобы поворачивать её на полу как нужно, - ну так физкультура нам полезна. Но нужно учитывать и такой момент: психотравма от осознания того, сколько же пыли и грязи собирается в жалких 33 квадраных метрах даже через два дня после последней уборки. В пылесосах с мешками этого не видно. Готовьте свои и котовьи неррррвы."
				};

				Review comment2 = new Review
				{
					UserId = userForComment2.Id,
					ItemId = item.Id,
					Rate = 5,
					Value = "Испытал сразу перед НГ. Мощно и хорошо пылесосит ламинат и ковры. Щетка прилипает к полу от мощности. Засосал битые стела, мишуру, пыль и грязь - все собралось в циклоне, а мешок остался практически чистым. Не знаю есть ли там защита от перегрева, но пол часа на полной мощности проработал. Модель называется SC20M255AWB. Модель 251 - без циклона, лучше не покупать."
				};


				Review comment = new Review
				{
					UserId = Guid.Parse("c368194e-3f8c-4753-b39a-cceedf7b59ec"),
					ItemId = item.Id,
					Value = "Долго выбирал между Лж, Бошем и Самсунгом. Посмотрел 'Контрольную закупку' про пылесосы с мешком, и выбрал этот, с регулятором( модель SC5610 дешевле, но без регулятора мощности) Покупал за 1800, хотел именно классик, который понимает одноразовые бумажные мешки, и эти мешки есть в продаже по адекватной цене.До этого был занусси без мешка(циклон) и моющий кёрхер.Циклон оказался непрактичным, малоёмким 1, 2л, дорогим по замене фильтра.Моющий, конечно, хорош, и свежесть, и влажность повышает, но с ним много мороки: уборка занимает минут 20, а мойка и чистка самого моющего пылесоса и всех ёмкостей, мойка ванной после мойки моющего пылесоса на полчаса, иначе пахнет плесенью"
				};

				Purchase purchase = new Purchase
				{
					UserId = Guid.Parse("c368194e-3f8c-4753-b39a-cceedf7b59ec"),
					ItemId = Guid.Parse("FD70A938-A363-4EE1-A9F7-986B0BA419FD")
				};

				using (var context = new ShopContext(connectionString))
				{
					//context.Users.Add(user);
					//context.Items.Add(item);
					//context.Purchases.Add(purchase);
					//context.Categories.Add(category);
					context.Reviews.Add(comment1);
					context.Reviews.Add(comment2);

					//var result = context.Categories.ToList();
					//context.Remove(category);

					//var quariedCategories = context.Categories.Where(x => x.CreationDate.Date < new System.DateTime(2017,10,5).Date);

					//var funnyResult = quariedCategories.Select(x => new
					//{
					//	Id = x.Id,
					//	StartDate = x.CreationDate,
					//	FunnyName = "Funny" + x.Name
					//});

					//var finalResult = funnyResult.ToList();

					context.SaveChanges();
				}
			}

			string data = "12345sdf";
			var newString = data.ExtractOnlyText();
		}


		static void Registration()
		{
			Console.Clear();
			string login, fullName, phoneNum, email, address, password, verCode;
			bool exist = false;
			Console.WriteLine("Введите Логин: ");
			login = Console.ReadLine();
			using (var context = new ShopContext(connectionString))
			{
				var result = from user_
										 in context.Users
										 where user_.Login.Equals(login)
										 select user_;
				if (result.Count() != 0)
				{
					exist = true;
				}
			}
			if (exist)
			{
				while (exist)
				{
					WriteMessage("Ошибка! Введеный вами логин уже существует! Попробуйте ввести другой.");
					Console.WriteLine("Введите Логин: ");
					login = Console.ReadLine();
					using (var context = new ShopContext(connectionString))
					{
						var result = from user_
												 in context.Users
												 where user_.Login.Equals(login)
												 select user_;
						if (result.Count() != 0)
						{
							exist = true;
						}
						else
						{
							exist = false;
						}
					}
				}
			}

			Console.WriteLine("Введите ФИО: ");
			fullName = Console.ReadLine();
			Console.WriteLine("Введите почту: ");
			email = Console.ReadLine();
			Console.WriteLine("Введите номер телефона: ");
			phoneNum = Console.ReadLine();
			Console.WriteLine("Введите адрес: ");
			address = Console.ReadLine();
			Console.WriteLine("Введите пароль: ");
			password = ConsoleReadLineButHashed();
			Console.WriteLine();
			password = bCryptHasher.EncryptPassword(password);
			Console.WriteLine("Введите секретный код (****): ");
			verCode = ConsoleReadLineButHashed();
			verCode = bCryptHasher.EncryptPassword(verCode);


			User user = new User
			{
				Login = login,
				FullName = fullName,
				PhoneNumber = phoneNum,
				Email = email,
				Address = address,
				Password = password,
				VerificationCode = verCode
			};
			using (var context = new ShopContext(connectionString))
			{
				context.Users.Add(user);
				context.SaveChanges();
			}
		}

		static void Categories()
		{
			int page = 1, pageSize = 2, pages = 1, categories = 0, itemsPageSize = 2;
			List<Category> categoriesList = null;

			using (var context = new ShopContext(connectionString))
			{
				var query = from category in context.Categories
										select category;
				categories = query.Count();
			}
			pages = categories / pageSize;
			if (categories % pageSize != 0) // Если кол-во страниц выпало как 5/3 то выйдет лишь 1 страница, поэтому добавляем еще одну
			{
				pages++;
			}
			int chooseCategoryOrPageAnswer = -1;
			while (chooseCategoryOrPageAnswer != 0)
			{
				int categoryNum;
				CategoryPage(ref page, pageSize, pages, ref categoriesList);
				Console.WriteLine("1. Выбрать категорию.");
				Console.WriteLine("2. Выбрать страницу.");
				Console.WriteLine("0. Вернуться в главное меню.");
				if (Int32.TryParse(Console.ReadLine(), out chooseCategoryOrPageAnswer) == false || chooseCategoryOrPageAnswer > 2 || chooseCategoryOrPageAnswer < 0)
				{
					chooseCategoryOrPageAnswer = -1;  // При выпадении false в Int32.TryParse параметр out page присваивается значние 0, и с новым циклом программа завершает работу
					WriteMessage("Введенное действие не является корректным. Выберите действие из списка!");
					continue;
				}
				switch (chooseCategoryOrPageAnswer)
				{
					case 1:
						Console.WriteLine($"Введите номер категории (1 - {pageSize}):");
						if (Int32.TryParse(Console.ReadLine(), out categoryNum) == false || categoryNum == 0)
						{
							WriteMessage("Введенное действие не является корректным. Возврат в выбор товара");
							continue;
						}
						if (categoryNum < 0)
						{
							categoryNum = -categoryNum;
						}
						int categoriesOnPage;
						using (var context = new ShopContext(connectionString))           // подсчет кол-ва записей на странице
						{
							var query = from category in context.Categories
													select category;
							query = query.Skip((page - 1) * pageSize).Take(pageSize);
							categoriesOnPage = query.Count();
						}
						if (categoryNum > pageSize || categoryNum > categoriesOnPage)
						{
							WriteMessage("Данный номер категории отсутсвует на странице.");
							continue;
						}
						Category choosenCategory;
						using (var context = new ShopContext(connectionString))
						{
							var query = from category in context.Categories
													select category;
							query = query.Skip((page - 1) * pageSize).Take(pageSize).Skip(categoryNum - 1).Take(1); // categoryNum - 1 т.к для того чтобы взять 3 элемент нужно пропустить лишь 2 и уже взять (Take) следующий 
							choosenCategory = query.ToList().First();
						}
						int categoriesCount;
						using (var context = new ShopContext(connectionString))
						{
							var query = from item in context.Items
													where item.CategoryId.Equals(choosenCategory.Id)
													select item;
							categoriesCount = query.Count();
						}
						if (categoriesCount == 0)
						{
							WriteMessage("По вашему запросу не найдено ни одного товара.");
							continue;
						}
						pages = categoriesCount / pageSize;
						if (categoriesCount % pageSize != 0) // Если кол-во страниц выпало как 5/3 то выйдет лишь 1 страница, поэтому добавляем еще одну
						{
							pages++;
						}
						int itemsPage = 1, itemNum;
						int chooseItemOrPageAnswer = -1;
						while (chooseItemOrPageAnswer != 0)
						{
							ShopPage(itemsPage, itemsPageSize, pages, choosenCategory);
							Console.WriteLine("1. Выбрать товар.");
							Console.WriteLine("2. Выбрать страницу.");
							Console.WriteLine("0. Вернуться к выбору категории.");
							if (Int32.TryParse(Console.ReadLine(), out chooseItemOrPageAnswer) == false)
							{
								chooseItemOrPageAnswer = -1;  // При выпадении false в Int32.TryParse параметр out page присваивается значние 0, и с новым циклом программа завершает работу
								WriteMessage("Введенное действие не является корректным. Выберите действие из списка!");
								continue;
							}
							int itemsOnPage = 0;
							switch (chooseItemOrPageAnswer)
							{
								case 0:
									Console.Clear();
									continue;
								case 1:
									Console.WriteLine($"Введите товар (1 - {pageSize}):");
									if (Int32.TryParse(Console.ReadLine(), out itemNum) == false || itemNum == 0)
									{
										WriteMessage("Введенное действие не является корректным. Возврат в выбор товара");
										continue;
									}
									if (itemNum < 0)
									{
										itemNum = -itemNum;
									}
									using (var context = new ShopContext(connectionString))           // подсчет кол-ва записей на странице
									{
										var query = from item in context.Items
																where item.CategoryId.Equals(choosenCategory.Id)
																select item;
										query = query.Skip((itemsPage - 1) * pageSize).Take(pageSize);
										itemsOnPage = query.Count();
									}
									if (itemNum > pageSize || itemNum > itemsOnPage)
									{
										WriteMessage("Данный номер товара отсутсвует на странице.");
										continue;
									}
									int chooseCommentsOrBuyAnswer = -1, commentsPageSize = 2, comments = 1, commentsPage = 1, commentsPages = 1;
									List<Item> result = null;
									List<Review> commentList = null;
									while (chooseCommentsOrBuyAnswer != 0)
									{
										commentsPage = 1; // Есть вероятность что пользователь попадет в false в Int32.TryParse параметр out commentsPage, где commentsPage присваивается -1, что вскоре приведет к ошибке
										ChooseItem(itemNum, itemsPage, pageSize, choosenCategory, out result);
										Guid resultItemId = result.First().Id;
										Console.WriteLine("1. Посмотреть комментарии");
										Console.WriteLine("2. Приобрести товар");
										Console.WriteLine("3. Оставить комментарий");
										Console.WriteLine("0. Назад");
										if (Int32.TryParse(Console.ReadLine(), out chooseCommentsOrBuyAnswer) == false || chooseCommentsOrBuyAnswer < 0)
										{
											chooseCommentsOrBuyAnswer = -1;  // При выпадении false в Int32.TryParse параметр out page присваивается значние 0, и с новым циклом программа завершает работу
											WriteMessage("Введенное действие не является корректным. Возврат в выбор товара.");
											continue;
										}
										switch (chooseCommentsOrBuyAnswer)
										{
											case 0:
												continue;
											case 1:
												using (var context = new ShopContext(connectionString))
												{
													var queryCom = from comment in context.Reviews
																				 where comment.ItemId.Equals(resultItemId)
																				 select comment;
													comments = queryCom.Count();
												}
												if (comments == 0)
												{
													WriteMessage("По вашему запросу не найдено ни одного комментария.");
													continue;
												}
												commentsPages = comments / commentsPageSize;
												if (comments % commentsPageSize != 0) // Если кол-во страниц выпало как 5/3 то выйдет лишь 1 страница, поэтому добавляем еще одну
												{
													commentsPages++;
												}
												CommentPage(commentsPages, commentsPageSize, commentsPage, resultItemId, commentList);
												while (commentsPage != 0)
												{
													Console.WriteLine($"Введите страницу (1 - {commentsPages}; 0 - Назад):");
													if (Int32.TryParse(Console.ReadLine(), out commentsPage) == false)
													{
														commentsPage = -1;  // При выпадении false в Int32.TryParse параметр out page присваивается значние 0, и с новым циклом программа завершает работу
														WriteMessage("Введенная страница не является числом.");
														continue;
													}
													if (commentsPage == 0)
													{
														continue;
													}
													CommentPage(commentsPages, commentsPageSize, commentsPage, resultItemId, commentList);
												}
												break;
											case 2:
												int price;
												string verKey;
												resultItemId = result.First().Id;
												int itemQuantity;
												using (var context = new ShopContext(connectionString))
												{
													var queryCom = from item in context.Items
																				 where item.Id.Equals(resultItemId)
																				 select item;
													itemQuantity = queryCom.First().Quantity;
													price = queryCom.First().Price;
												}
												if (itemQuantity == 0)
												{
													WriteMessage("Товар не доступен к приобретению, так как закончился на складе");
													break;
												}
												if (user.Balance >= price)
												{
													Console.WriteLine("На ваш номер телефона отправлен код подтверждения платежа и необходимая информация.");
													Console.WriteLine("Введите код подтверждения:");
													verKey = Console.ReadLine();
													user.Balance -= price;
													UpdateUser();
													Purchase purchase = new Purchase
													{
														ItemId = resultItemId,
														UserId = user.Id
													};
													using (var context = new ShopContext(connectionString))
													{
														var shortQuery = context.Purchases.Add(purchase);
														context.SaveChanges();
													}
													Minus1ToQuantity(resultItemId);
													Console.WriteLine("Платеж успешно совершен.");
													Console.ReadLine();
												}
												else
												{
													WriteMessage("На счету недостаточно средств");
												}
												break;
											case 3:
												int purchases = 0;
												using (var context = new ShopContext(connectionString))
												{
													var checkIfHeBoughtThisItem = from purchase in context.Purchases
																												where purchase.ItemId.Equals(resultItemId) & purchase.UserId.Equals(user.Id)
																												select purchase;
													purchases = checkIfHeBoughtThisItem.Count();
												}
												if (purchases == 0)
												{
													WriteMessage("Вы никогда не приобретали этот товар. Вы не можете оставить комментарий");
													break;
												}
												int reviewRate = 0;
												Console.WriteLine("Введите оценку продукту (1-5)");
												if (Int32.TryParse(Console.ReadLine(), out reviewRate) == false && (reviewRate < 0 || reviewRate > 5))
												{
													bool wrong = true;
													while (wrong)
													{
														WriteMessage("Введена некорректная оценка. Введите новую оценку");
														Console.WriteLine("Введите оценку продукту(1-5)");
														if (Int32.TryParse(Console.ReadLine(), out reviewRate) == true && (reviewRate >= 0 && reviewRate <= 5))
														{
															wrong = false;
														}
													}
												}
												Console.WriteLine("Введите ваш комментарий");
												string reviewText;
												reviewText = Console.ReadLine();
												Review userReview = new Review
												{
													ItemId = resultItemId,
													Rate = reviewRate,
													UserId = user.Id,
													Value = reviewText
												};
												using (var context = new ShopContext(connectionString))
												{
													var shortQuery = context.Reviews.Add(userReview);
													context.SaveChanges();
												}
												Console.WriteLine("Комментарий успешно добавлен");
												Console.ReadLine();
												break;
										}
									}
									break;
								case 2:
									Console.WriteLine($"Введите страницу (1 - {pages}):");
									if (Int32.TryParse(Console.ReadLine(), out itemsPage) == false || itemsPage <= 0 || itemsPage > pages)
									{
										itemsPage = 1;
										WriteMessage("Введен некорректный номер страницы.");
										continue;
									}
									ShopPage(itemsPage, pageSize, pages, choosenCategory);
									break;
							}
						}
						break;
					case 2:

						Console.WriteLine($"Введите страницу (1 - {pages}):");
						if (Int32.TryParse(Console.ReadLine(), out page) == false || page <= 0)
						{
							page = 1;
							WriteMessage("Введен некорректный номер страницы.");
							continue;
						}
						CategoryPage(ref page, pageSize, pages, ref categoriesList);
						break;
					case 0:
						Console.Clear();
						chooseCategoryOrPageAnswer = 0;
						continue;
				}
			}
		}

		private static void Minus1ToQuantity(Guid resultItemId)
		{
			using (var context = new ShopContext(connectionString))
			{
				var shortQuery = from item in context.Items
												 where item.Id.Equals(resultItemId)
												 select item;
				var thatItem = shortQuery.First();
				thatItem.Quantity = thatItem.Quantity - 1;
				context.Update(thatItem);
				context.SaveChanges();
			}
		}

		private static List<Item> ChooseItem(int toSkip, int page, int pageSize, Category choosenCategory, out List<Item> result)
		{
			Console.Clear();
			toSkip--; // Если мы берем 1ый Айтем, то нужно сделать 0 скипов на странице, так как сразу берется(Take) первый же, и так далее
			using (var context = new ShopContext(connectionString))
			{
				var query = from item in context.Items
										where item.CategoryId.Equals(choosenCategory.Id)
										select item;
				query = query.Skip((page - 1) * pageSize).Take(pageSize).Skip(toSkip).Take(1);
				result = query.ToList();
			}
			Guid resultItemId = result.First().Id;
			int reviews;
			List<Review> reviewList;
			using (var context = new ShopContext(connectionString))
			{
				var queryReview = from comment in context.Reviews
													where comment.ItemId.Equals(resultItemId)
													select comment;
				reviewList = queryReview.ToList();
				reviews = queryReview.Count();
			}
			double avrgRate = 0;
			foreach (var review in reviewList)
			{
				avrgRate += review.Rate;
			}
			avrgRate /= (double)reviewList.Count;
			foreach (var item in result)
			{
				Console.WriteLine($"\tНаименование: {item.Name}");
				Console.WriteLine($"\tИзображение: {item.ImagePath}");
				Console.WriteLine($"\tУникальный идентификатор: {item.Id}");
				Console.WriteLine($"\tЦена: {item.Price}");
				Console.WriteLine($"\tОписание: {item.Description}");
				Console.WriteLine($"\tНа складе: {item.Quantity}");
				Console.WriteLine($"\tОтзывов - {reviews}");
				Console.WriteLine($"\tОбщий рейтинг - {avrgRate.ToString("0.0")}");
			}

			return result;
		}

		private static int ShopPage(int page, int pageSize, int pages, Category choosenCategory)
		{
			Console.Clear();
			List<Item> result;
			if (page < 0)
			{
				page = -page;
			}
			if (page > pages)
			{
				Console.WriteLine("Введенной страницы не существует.");
				Console.ReadLine();
				Console.Clear();
			}
			using (var context = new ShopContext(connectionString))
			{
				var query = from item in context.Items
										where item.CategoryId.Equals(choosenCategory.Id)
										select item;
				var paging = query.Skip((page - 1) * pageSize).Take(pageSize);
				result = paging.ToList();
			}
			Console.WriteLine($"Page {page}/{pages}: ");
			int num = 1;
			foreach (var item in result)
			{
				Console.WriteLine($"\t{num++}) {item.Name}");
			}
			return page;
		}

		public static void CategoryPage(ref int page, int pageSize, int pages, ref List<Category> categoriesList)
		{
			Console.Clear();
			if (page < 0)
			{
				page = -page;
			}
			if (page > pages)
			{
				WriteMessage("Введенной страницы не существует.");
				page = 1;
				return;
			}
			using (var context = new ShopContext(connectionString))
			{
				var query = from category in context.Categories
										select category;
				var paging = query.Skip((page - 1) * pageSize).Take(pageSize);
				categoriesList = paging.ToList();
			}
			Console.WriteLine($"Page {page}/{pages}:");
			int num = 1;
			foreach (var category in categoriesList)
			{
				Console.WriteLine($"\t{num++}) {category.Name}");
			}
		}

		static void Search()
		{
			int page = 1, pageSize = 2, pages = 1, items = 0, chooseItemOrPageAnswer, itemNum;
			string search = null;
			List<Item> result = null;
			IQueryable<Item> query;
			while (search != "0")
			{
				page = 1; // Есть вероятность что пользователь попадет в false в Int32.TryParse параметр out page, где page присваивается -1, что вскоре приведет к ошибке
				Console.Clear();
				Console.WriteLine("Введите искомый товар (0 - Назад):");
				search = Console.ReadLine();
				if (search == "0")
				{
					Console.Clear();
					continue;
				}
				using (var context = new ShopContext(connectionString))
				{
					query = from item in context.Items
									where item.Name.Contains(search)
									select item;
					items = query.Count();
				}
				if (items == 0)
				{
					WriteMessage("По вашему запросу не найдено ни одного товара.");
					continue;
				}
				pages = items / pageSize;
				if (items % pageSize != 0) // Если кол-во страниц выпало как 5/3 то выйдет лишь 1 страница, поэтому добавляем еще одну
				{
					pages++;
				}

				while (page != 0)
				{
					ShopPage(page, pageSize, pages, search, result, query);
					Console.WriteLine("1. Выбрать товар.");
					Console.WriteLine("2. Выбрать страницу.");
					Console.WriteLine("0. Вернуться к поиску товаров.");
					if (Int32.TryParse(Console.ReadLine(), out chooseItemOrPageAnswer) == false)
					{
						chooseItemOrPageAnswer = -1;  // При выпадении false в Int32.TryParse параметр out page присваивается значние 0, и с новым циклом программа завершает работу
						WriteMessage("Введенное действие не является корректным. Выберите действие из списка!");
						continue;
					}
					switch (chooseItemOrPageAnswer)
					{
						case 0:
							page = 0;
							Console.Clear();
							continue;
						case 1:
							Console.WriteLine($"Введите товар (1 - {pageSize}):");
							if (Int32.TryParse(Console.ReadLine(), out itemNum) == false || itemNum == 0)
							{
								WriteMessage("Введенное действие не является корректным. Возврат в выбор товара");
								continue;
							}
							if (itemNum < 0)
							{
								itemNum = -itemNum;
							}
							int itemsOnPage;
							using (var context = new ShopContext(connectionString))           // подсчет кол-ва записей на странице
							{
								query = from item in context.Items
												where item.Name.Contains(search)
												select item;
								query = query.Skip((page - 1) * pageSize).Take(pageSize);
								itemsOnPage = query.Count();
							}
							if (itemNum > pageSize || itemNum > itemsOnPage)
							{
								WriteMessage("Данный номер товара отсутсвует на странице.");
								continue;
							}
							ItemInformation(page, pageSize, itemNum, search);
							break;
						case 2:
							Console.WriteLine($"Введите страницу (1 - {pages}):");
							if (Int32.TryParse(Console.ReadLine(), out page) == false)
							{
								page = -1;  // При выпадении false в Int32.TryParse параметр out page присваивается значние 0, и с новым циклом программа завершает работу
								WriteMessage("Введенная страница не является числом.");
								continue;
							}
							ShopPage(page, pageSize, pages, search, result, query);
							break;
					}
				}
			}
		}

		private static void ItemInformation(int page, int pageSize, int itemNum, string search)
		{
			int chooseCommentsOrBuyAnswer = -1, commentsPageSize = 2, comments = 1, commentsPage = 1, commentsPages = 1;
			List<Item> result = null;
			List<Review> commentList = null;
			while (chooseCommentsOrBuyAnswer != 0)
			{
				commentsPage = 1; // Есть вероятность что пользователь попадет в false в Int32.TryParse параметр out commentsPage, где commentsPage присваивается -1, что вскоре приведет к ошибке
				ChooseItem(itemNum, page, pageSize, search, out result);
				Guid resultItemId = result.First().Id;
				Console.WriteLine("1. Посмотреть комментарии");
				Console.WriteLine("2. Приобрести товар");
				Console.WriteLine("3. Оставить комментарий");
				Console.WriteLine("0. Назад");
				if (Int32.TryParse(Console.ReadLine(), out chooseCommentsOrBuyAnswer) == false || chooseCommentsOrBuyAnswer < 0)
				{
					chooseCommentsOrBuyAnswer = -1;  // При выпадении false в Int32.TryParse параметр out page присваивается значние 0, и с новым циклом программа завершает работу
					WriteMessage("Введенное действие не является корректным. Возврат в выбор товара.");
					continue;
				}
				switch (chooseCommentsOrBuyAnswer)
				{
					case 0:
						continue;
					case 1:
						using (var context = new ShopContext(connectionString))
						{
							var queryCom = from comment in context.Reviews
														 where comment.ItemId.Equals(resultItemId)
														 select comment;
							comments = queryCom.Count();
						}
						if (comments == 0)
						{
							WriteMessage("По вашему запросу не найдено ни одного комментария.");
							continue;
						}
						commentsPages = comments / commentsPageSize;
						if (comments % commentsPageSize != 0) // Если кол-во страниц выпало как 5/3 то выйдет лишь 1 страница, поэтому добавляем еще одну
						{
							commentsPages++;
						}
						CommentPage(commentsPages, commentsPageSize, commentsPage, resultItemId, commentList);
						while (commentsPage != 0)
						{
							Console.WriteLine($"Введите страницу (1 - {commentsPages}; 0 - Назад):");
							if (Int32.TryParse(Console.ReadLine(), out commentsPage) == false)
							{
								commentsPage = -1;  // При выпадении false в Int32.TryParse параметр out page присваивается значние 0, и с новым циклом программа завершает работу
								WriteMessage("Введенная страница не является числом.");
								continue;
							}
							if (commentsPage == 0)
							{
								continue;
							}
							CommentPage(commentsPages, commentsPageSize, commentsPage, resultItemId, commentList);
						}
						break;
					case 2:
						int price;
						string verKey;
						resultItemId = result.First().Id;
						int itemQuantity;
						using (var context = new ShopContext(connectionString))
						{
							var queryCom = from item in context.Items
														 where item.Id.Equals(resultItemId)
														 select item;
							itemQuantity = queryCom.First().Quantity;
							price = queryCom.First().Price;
						}
						if (itemQuantity == 0)
						{
							WriteMessage("Товар не доступен к приобретению, так как закончился на складе");
							break;
						}
						if (user.Balance >= price)
						{
							Console.WriteLine("На ваш номер телефона отправлен код подтверждения платежа и необходимая информация.");
							Console.WriteLine("Введите код подтверждения:");
							verKey = Console.ReadLine();
							user.Balance -= price;
							UpdateUser();
							Purchase purchase = new Purchase
							{
								ItemId = resultItemId,
								UserId = user.Id
							};
							using (var context = new ShopContext(connectionString))
							{
								var shortQuery = context.Purchases.Add(purchase);
								context.SaveChanges();
							}
							Minus1ToQuantity(resultItemId);
							Console.WriteLine("Платеж успешно совершен.");
							Console.ReadLine();
						}
						else
						{
							WriteMessage("На счету недостаточно средств");
						}
						break;
					case 3:
						int purchases = 0;
						using (var context = new ShopContext(connectionString))
						{
							var checkIfHeBoughtThisItem = from purchase in context.Purchases
																						where purchase.ItemId.Equals(resultItemId) & purchase.UserId.Equals(user.Id)
																						select purchase;
							purchases = checkIfHeBoughtThisItem.Count();
						}
						if (purchases == 0)
						{
							WriteMessage("Вы никогда не приобретали этот товар. Вы не можете оставить комментарий");
							break;
						}
						int reviewRate = 0;
						Console.WriteLine("Введите оценку продукту(1-5)");
						if (Int32.TryParse(Console.ReadLine(), out reviewRate) == false && (reviewRate < 0 || reviewRate > 5))
						{
							bool wrong = true;
							while (wrong)
							{
								WriteMessage("Введена некорректная оценка. Введите новую оценку");
								Console.WriteLine("Введите оценку продукту(1-5)");
								if (Int32.TryParse(Console.ReadLine(), out reviewRate) == true && (reviewRate >= 0 && reviewRate <= 5))
								{
									wrong = false;
								}
							}
						}
						Console.WriteLine("Введите ваш комментарий");
						string reviewText;
						reviewText = Console.ReadLine();
						Review userReview = new Review
						{
							ItemId = resultItemId,
							Rate = reviewRate,
							UserId = user.Id,
							Value = reviewText
						};
						using (var context = new ShopContext(connectionString))
						{
							var shortQuery = context.Reviews.Add(userReview);
							context.SaveChanges();
						}
						Console.WriteLine("Комментарий успешно добавлен");
						Console.ReadLine();
						break;
				}
			}
		}

		private static void CommentPage(int commentsPages, int commentsPageSize, int commentsPage, Guid resultItemId, List<Review> commentList)
		{
			Console.Clear();
			if (commentsPage < 0)
			{
				commentsPage = -commentsPage;
			}
			if (commentsPage > commentsPages)
			{
				Console.WriteLine("Введенной страницы не существует.");
				Console.ReadLine();
				Console.Clear();
				return;
			}
			using (var context = new ShopContext(connectionString))
			{

				var queryCom = from comment in context.Reviews
											 where comment.ItemId.Equals(resultItemId)
											 select comment;
				var paging = queryCom.Skip((commentsPage - 1) * commentsPageSize).Take(commentsPageSize);
				commentList = paging.ToList();
			}
			Console.WriteLine($"Page {commentsPage}/{commentsPages}:");

			string Login;
			using (var context = new ShopContext(connectionString))
			{
				var userComment = context.Users.Join(context.Reviews, // второй набор
				user => user.Id, // свойство-селектор объекта из первого набора
				review => review.UserId, // свойство-селектор объекта из второго набора
				(u, r) => new // результат
				{
					Login = u.Login
				});
				Login = userComment.First().Login;
			}
			int num = 1;
			foreach (var comment in commentList)
			{
				Console.WriteLine($"{num++})Пользователь {Login} оставил комментарий ({comment.Rate}/5):");
				Console.Write("\t");
				Console.WriteLine(DivideComment(comment.Value));
			}
		}

		private static string DivideComment(string str)
		{
			String[] sublines = str.Split(' ');
			str = null;
			int length = 80; //длина разбиения
			int j = 0;
			for (int i = 0; i < sublines.Count(); i++)
			{
				if (j + sublines[i].Length < length)
				{
					str = str + sublines[i] + " ";
					j = j + sublines[i].Length;
				}
				else
				{
					j = 0;
					str = str + "\r\n\t";
					i--;
				}
			}
			return str;
		}

		private static void WriteMessage(string message)
		{
			Console.Clear();
			Console.WriteLine(message);
			Console.ReadLine();
			Console.Clear();
		}

		private static void ChooseItem(int toSkip, int page, int pageSize, string search, out List<Item> result)
		{
			Console.Clear();
			toSkip--; // Если мы берем 1ый Айтем, то нужно сделать 0 скипов на странице, так как сразу берется(Take) первый же, и так далее
			using (var context = new ShopContext(connectionString))
			{
				var query = from item in context.Items
										where item.Name.Contains(search)
										select item;
				query = query.Skip((page - 1) * pageSize).Take(pageSize).Skip(toSkip).Take(1);
				result = query.ToList();
			}
			Guid resultItemId = result.First().Id;
			int reviews;
			List<Review> reviewList;
			using (var context = new ShopContext(connectionString))
			{
				var queryReview = from comment in context.Reviews
													where comment.ItemId.Equals(resultItemId)
													select comment;
				reviewList = queryReview.ToList();
				reviews = queryReview.Count();
			}
			double avrgRate = 0;
			foreach (var review in reviewList)
			{
				avrgRate += review.Rate;
			}
			avrgRate /= (double)reviewList.Count;
			foreach (var item in result)
			{
				Console.WriteLine($"\tНаименование: {item.Name}");
				Console.WriteLine($"\tИзображение: {item.ImagePath}");
				Console.WriteLine($"\tУникальный идентификатор: {item.Id}");
				Console.WriteLine($"\tЦена: {item.Price}");
				Console.WriteLine($"\tОписание: {item.Description}");
				Console.WriteLine($"\tНа складе: {item.Quantity}");
				Console.WriteLine($"\tОтзывов - {reviews}");
				Console.WriteLine($"\tОбщий рейтинг - {avrgRate.ToString("0.0")}");
			}
		}

		private static void ShopPage(int page, int pageSize, int pages, string search, List<Item> result, IQueryable<Item> query)
		{
			Console.Clear();
			if (page < 0)
			{
				page = -page;
			}
			if (page > pages)
			{
				Console.WriteLine("Введенной страницы не существует.");
				Console.ReadLine();
				Console.Clear();
				return;
			}
			using (var context = new ShopContext(connectionString))
			{
				query = from item in context.Items
								where item.Name.Contains(search)
								select item;
				var paging = query.Skip((page - 1) * pageSize).Take(pageSize);
				result = paging.ToList();
			}
			Console.WriteLine($"Page {page}/{pages}:");
			int num = 1;
			foreach (var item in result)
			{
				Console.WriteLine($"\t{num++}) {item.Name} - {item.Id}");
			}
		}

		static void Pagination()
		{
			int page = 0, pageSize = 3;
			List<Item> result;
			while (page != 0)
			{
				Console.WriteLine("Введите страницу (0 для выхода):");

				if (!Int32.TryParse(Console.ReadLine(), out page))
				{
					Console.WriteLine("Введенная страница не является числом.");
					Console.ReadLine();
					Console.Clear();
					continue;
				}
				Console.Clear();
				using (var context = new ShopContext(connectionString))
				{
					var query = from item in context.Items
											orderby item.Name
											select item;

					var paging = query.Skip((page - 1) * pageSize).Take(pageSize);

					result = paging.ToList();
				}
				Console.WriteLine($"Page {page}:");
				foreach (var item in result)
				{
					Console.WriteLine($"\t{item.Name} - {item.Price} тг");
				}
			}
		}
	}
}