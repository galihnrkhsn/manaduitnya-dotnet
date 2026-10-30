Method                          |	Mengembalikan                                       |	Dipakai untuk
ExecuteAsync                    |	int (jumlah baris terpengaruh)                      |	INSERT/UPDATE/DELETE tanpa butuh data balik
ExecuteScalarAsync<T>           |	satu nilai (kolom pertama baris pertama)            |	COUNT, EXISTS, RETURNING id
QueryAsync<T>                   |	IEnumerable<T>                                      |	SELECT banyak baris (list)
QueryFirstOrDefaultAsync<T>     |	satu baris atau null                                |	SELECT satu baris, boleh tidak ada
QueryFirstAsync<T>              |	satu baris, error jika kosong                       |	SELECT satu baris yang pasti ada
QuerySingleAsync<T>             |	satu baris, error jika kosong atau lebih dari satu  |	harus tepat satu baris
QuerySingleOrDefaultAsync<T>    |	satu baris atau null, error jika lebih dari satu    |	maksimal satu baris