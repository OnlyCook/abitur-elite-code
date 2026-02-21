import tkinter as tk

PLANT_MARK = "__PLANTUML_N__"

def one_liner_to_pretty(text: str) -> str:
    text = text.replace("\\n/", PLANT_MARK)
    text = text.replace("\\n", "\n")
    text = text.replace('\\"', '"')
    text = text.replace(PLANT_MARK, "\\n")
    return text

def pretty_to_one_liner(text: str) -> str:
    text = text.replace("\r\n", "\n").replace("\r", "\n")
    text = text.replace("\\n", PLANT_MARK)
    text = text.replace('"', '\\"')
    text = text.replace("\n", "\\n")
    text = text.replace(PLANT_MARK, "\\n/")
    return text

def paste_pretty():
    txt = root.clipboard_get()
    pretty_box.delete("1.0", tk.END)
    pretty_box.insert(tk.END, txt)
    one_box.delete("1.0", tk.END)
    one_box.insert(tk.END, pretty_to_one_liner(txt))

def copy_pretty():
    txt = pretty_box.get("1.0", tk.END).rstrip("\n")
    root.clipboard_clear()
    root.clipboard_append(txt)

def paste_one():
    txt = root.clipboard_get()
    one_box.delete("1.0", tk.END)
    one_box.insert(tk.END, txt)
    pretty_box.delete("1.0", tk.END)
    pretty_box.insert(tk.END, one_liner_to_pretty(txt))

def copy_one():
    txt = one_box.get("1.0", tk.END).rstrip("\n")
    root.clipboard_clear()
    root.clipboard_append(txt)


root = tk.Tk()
root.title("Plantuml Oneliner Converter")

# layout
frame = tk.Frame(root)
frame.pack(padx=8, pady=8)

# pretty
tk.Label(frame, text="Pretty Plantuml").grid(row=0, column=0, sticky="w")
pretty_box = tk.Text(frame, width=60, height=20)
pretty_box.grid(row=1, column=0, padx=5, pady=5)

btns_pretty = tk.Frame(frame)
btns_pretty.grid(row=2, column=0, pady=(0, 10))
tk.Button(btns_pretty, text="Paste Pretty", command=paste_pretty).pack(side="left", padx=5)
tk.Button(btns_pretty, text="Copy Pretty", command=copy_pretty).pack(side="left", padx=5)

# oneliner
tk.Label(frame, text="Oneliner").grid(row=0, column=1, sticky="w")
one_box = tk.Text(frame, width=60, height=20)
one_box.grid(row=1, column=1, padx=5, pady=5)

btns_one = tk.Frame(frame)
btns_one.grid(row=2, column=1, pady=(0, 10))
tk.Button(btns_one, text="Paste Oneliner", command=paste_one).pack(side="left", padx=5)
tk.Button(btns_one, text="Copy Oneliner", command=copy_one).pack(side="left", padx=5)

root.mainloop()